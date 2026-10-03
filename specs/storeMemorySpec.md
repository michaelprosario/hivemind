# Tech Plan: Store Agent Memory in a Named Index

## Requirement
As an AI agent, I should be able to store data into a named index.

- Input: `text`, metadata (`tags`, `timestamps`, `author`, `source URL`), optional `key`/`id`.
- Purpose: stores unstructured data, code snippets, user preferences, or task progress — i.e. general-purpose agent "memory", not just files on disk.

## Current state
- `IVectorDocumentService.IngestAsync` (`src/FileRepo.Core/VectorSearch/VectorDocumentService.cs`) only accepts a `SourcePath`: it reads a file from disk (`ISourceTextReader`), chunks it, embeds the chunks, and stores them via `IVectorDocumentStore.IngestAsync`, which dedupes by content hash against a JSON catalog keyed on `(index, canonicalSourcePath)` and does an atomic swap (`isActive` payload flag + delete-old-version filter). There is no way to ingest raw text supplied inline, no key/id-based upsert, and no metadata fields beyond `sourcePath`/`sourceName`/`contentHash`/`documentVersion`/`chunkIndex`/`modelRevision`.
- `QdrantVectorDocumentStore` (`src/FileRepo.Infrastructure/VectorSearch/QdrantVectorDocumentStore.cs`) creates collections on demand (`EnsureCollectionAsync`) with `vectors.size`/`Cosine` and a keyword index on `sourcePath`. Points carry a fixed payload shape; there is no generic metadata bag.
- MCP tools (`src/FileRepo.Mcp/Tools/VectorSearchTools.cs`) expose `Search`, `ListIndexes`; file ingestion (`ingest`, `ingest-directory`) is only wired up via the Web API/CLI today, not MCP.
- Web API parity exists in `VectorDocumentsController` (`src/FileRepo.Web/Controllers/VectorDocumentsController.cs`) / `VectorSearchContracts.cs`.
- Follows `specs/cleanArchitecture.md`: MCP/Web -> Core service -> Core port -> Infrastructure adapter.

## Design

### New use case: `StoreMemoryAsync`
A new, separate use case from file ingestion — this is for text supplied directly by the caller (an AI agent), not a file path.

- `IVectorDocumentService.StoreMemoryAsync(StoreMemoryCommand, ct) -> AppResult<StoredMemory>`
- `StoreMemoryCommand(string VectorIndexName, string Text, string? Key, IReadOnlyList<string>? Tags, string? Author, string? SourceUrl)`
  - `Key` is optional. If supplied, it is the caller-chosen idempotent identity for the memory (upsert semantics: storing again with the same key replaces the prior content/metadata). If omitted, a new id is generated (`Guid.NewGuid()`), i.e. always inserts a new memory.
  - `Tags`, `Author`, `SourceUrl` are optional metadata; `CreatedAt`/`UpdatedAt` timestamps are set by the server (UTC, not caller-supplied) so they're trustworthy for later filtering/sorting.
- `StoredMemory(string VectorIndexName, string Key, string ContentHash, int ChunkCount, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt)` — result model, mirrors `IngestedVectorDocument` shape.

### Validation (`VectorDocumentService`)
- Reuse `ValidateIndex` for `vectorIndexName`.
- `Text`: required, non-whitespace; cap length (new `MaxMemoryTextLength` in `VectorSearchValidationRules`, e.g. 200,000 chars) to avoid unbounded embedding cost — return `Invalid` above the cap (mirrors `MaxQueryLength` handling for search).
- `Key`: optional; if present, must be a reasonable identifier — reuse a conservative pattern, e.g. `^[A-Za-z0-9][A-Za-z0-9_.:-]{0,127}$`, so it can be hashed to a deterministic point/group id and used safely in Qdrant filters.
- `Tags`: optional list of short strings (trim, dedupe, cap count e.g. 32 and length e.g. 64 chars each) — invalid entries are a validation error, not silently dropped.
- `SourceUrl`: optional; if present, must parse as `Uri` (absolute) — validation error otherwise.
- `Author`: optional free text, length-capped (e.g. 256 chars).

### Chunking & embedding
- Reuse `ITextChunker`/`ITextEmbeddingProvider` exactly as ingestion does: chunk `Text`, embed each chunk. Most memories will be short (one chunk), but code snippets/long notes may span multiple chunks — keeping this path identical to file ingestion avoids a second chunking strategy and keeps `Search` behavior (which already filters `isActive=true`) consistent across both ingestion paths.

### Store port (`IVectorDocumentStore`)
- New method: `StoreMemoryAsync(string vectorIndexName, StoredMemoryRecord record, IReadOnlyList<VectorChunk> chunks, IReadOnlyList<ReadOnlyMemory<float>> vectors, int vectorSize, CancellationToken ct) -> AppResult<StoredMemory>`.
- `QdrantVectorDocumentStore` implementation:
  - `EnsureCollectionAsync` as today (shared with file ingestion — same collection can hold both files and memories; they're distinguished by a payload `kind` field, see below).
  - Resolve the group id: if `Key` supplied, `groupId = DeterministicGuid($"memory\n{vectorIndexName}\n{Key}")`; else `groupId = Guid.NewGuid()`. Per-chunk point ids are deterministic from `(groupId, chunkIndex)` so re-storing the same key overwrites the same points (idempotent upsert) instead of accumulating orphan points.
  - Point payload: `{ kind: "memory", key, tags: string[], author, sourceUrl, createdAt, updatedAt, contentHash, chunkIndex, startCharacter, endCharacter, text, modelRevision, isActive: true }`. No catalog-file dedup (unlike file ingestion) — the point ids themselves make the write idempotent; `createdAt` is preserved on update by reading the existing point's payload (if present) before overwrite, `updatedAt` always refreshed.
  - Overwrite flow: `PUT /collections/{index}/points?wait=true` with the new chunk points (same ids as any previous write for that key), then `POST /points/delete` for any *extra* previous chunk ids beyond the new chunk count (handles text shrinking across updates) using a filter on `key` + `chunkIndex >= newChunkCount`.
  - Add a keyword payload index on `key` (alongside the existing `sourcePath` index) so filtering/deleting by key is efficient.
- `kind` discriminator keeps `Search` payload parsing backward compatible: `VectorSearchHit` parsing can default missing fields (`sourceName`/`sourcePath`) to empty/`key` for memory points, or `Search` can optionally expose a new `Kind`/`Key` field — see Core changes below.

### Core model changes (`VectorModels.cs`)
```csharp
public sealed record StoreMemoryCommand(
    string VectorIndexName,
    string Text,
    string? Key,
    IReadOnlyList<string>? Tags,
    string? Author,
    string? SourceUrl);

public sealed record StoredMemory(
    string VectorIndexName,
    string Key,
    string ContentHash,
    int ChunkCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
```
- Extend `VectorSearchHit` with nullable `Key`, `Tags`, `Author`, `SourceUrl`, `Kind` so `Search` can surface memory results alongside file results without a second search tool. Keep existing fields for file hits populated as today; memory hits leave `SourceName`/`CanonicalSourcePath` empty (or set `SourceName = Key`) and populate the new fields.
- Add `MaxMemoryTextLength` (and a `MaxTagCount`/`MaxTagLength`) to `VectorSearchValidationRules`.

### MCP tool (`VectorSearchTools.cs`)
```csharp
[McpServerTool, Description(
    "Stores text (notes, code snippets, preferences, task progress) as agent memory in a named vector index " +
    "so it can later be found via semantic search. Provide an idempotent key to update/overwrite an existing " +
    "memory; omit it to always create a new entry.")]
public static async Task<string> StoreMemory(
    IVectorDocumentService vectorDocumentService,
    [Description("Vector index (Qdrant collection) name to store into, e.g. 'agent_memory'.")]
    string vectorIndexName,
    [Description("The text content to store.")]
    string text,
    [Description("Optional idempotent key/id. Storing again with the same key replaces the prior content.")]
    string? key = null,
    [Description("Optional tags for filtering/organization.")]
    string[]? tags = null,
    [Description("Optional author identifier.")]
    string? author = null,
    [Description("Optional source URL this memory was derived from.")]
    string? sourceUrl = null,
    CancellationToken cancellationToken = default)
    => ToJson(await vectorDocumentService.StoreMemoryAsync(
        new(vectorIndexName, text, key, tags, author, sourceUrl), cancellationToken));
```
- Update the `Search` tool description to mention it can also return stored memories, and that `list_indexes` discovers index names shared by both files and memories.

### Web API (optional parity, same pattern as `ingest`)
- `POST /api/vector-documents/store-memory` in `VectorDocumentsController`, with `StoreMemoryRequest`/`StoreMemoryResponse` in `VectorSearchContracts.cs`, mirroring `Ingest`/`IngestVectorDocumentRequest`.

### Docs
- README: add `store_memory` to the MCP tools list with example request/response; note the `kind` distinction between file-ingested chunks and agent memories sharing an index.

## Testing
- Unit (`tests/FileRepo.Core.UnitTests`): `VectorDocumentService.StoreMemoryAsync` — validation errors (bad index name, empty text, text over limit, invalid key pattern, invalid tag, non-absolute source URL), successful store calls store with expected chunk/vector counts, propagates store failures via fake `IVectorDocumentStore`.
- Infrastructure: `QdrantVectorDocumentStore` with a stub `HttpMessageHandler` — new key creates a collection + points; re-storing same key overwrites points at the same ids and trims extra old chunks when text shrinks; omitted key always creates a new, distinct group id.
- Manual/integration: `docker-compose/qdrant` up, call `store_memory` twice with the same key (verify overwrite via `curl localhost:6333/collections/{name}/points/scroll`), then `search`/`list_indexes` to confirm the memory is retrievable and the index reports updated point counts.

## Acceptance criteria
- MCP exposes `store_memory`; storing with no `key` always creates a new memory, storing twice with the same `key` results in exactly one memory (latest content/metadata wins).
- Metadata (`tags`, `author`, `sourceUrl`, `createdAt`, `updatedAt`) is persisted and returned by `Search` hits for memory points.
- Invalid input (oversized text, malformed key, bad tag, non-absolute URL) returns a validation error, not a 500/exception.
- Build and existing tests still pass.

## Rollout / risks
- Additive change: new command/method/tool; no existing contract changes except widening `VectorSearchHit` with nullable fields (non-breaking for JSON consumers).
- Risk: sharing collections between file ingestion and memory storage means a vector-size/distance mismatch is still caught by `EnsureCollectionAsync`, but callers should use distinct index names per purpose if they want independent lifecycle/deletion.
- Risk: without a catalog-file equivalent, duplicate `Key` collisions across different callers are possible if keys aren't namespaced by caller — document that keys should be chosen carefully (e.g. prefixed) by the agent.

## Task order
1. Core models (`StoreMemoryCommand`, `StoredMemory`, `VectorSearchHit` extensions, validation rules) + interface methods.
2. `VectorDocumentService.StoreMemoryAsync` (validation + chunk/embed + delegate to store).
3. `QdrantVectorDocumentStore.StoreMemoryAsync` (group id derivation, upsert + trim old chunks, payload index on `key`).
4. Update `SearchAsync`/`VectorSearchHit` payload parsing to include memory metadata fields.
5. MCP tool `StoreMemory`.
6. Tests (Core unit + Infrastructure).
7. (Optional) Web endpoint + contracts.
8. README update.

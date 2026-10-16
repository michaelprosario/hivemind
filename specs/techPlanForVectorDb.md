# Tech Plan: Local Qdrant Vector Search

## 1. Summary

Add semantic ingestion and search to the File Repository API. A developer will
provide a path to a file available on the local file system. The system reads
the file, extracts supported text, divides the text into overlapping chunks,
generates an embedding for each chunk with a free local model, and stores the
vectors and source metadata in a local Qdrant collection.

A search request embeds its query with the same model and returns the requested
number of most similar chunks, including the original file name and enough
location metadata for the developer to find the source text.

This design follows [cleanArchitecture.md](./cleanArchitecture.md): Core owns
use cases, models, commands, queries, and abstractions; Infrastructure owns
the file system, embedding model, and Qdrant client; Web owns HTTP contracts
and dependency composition.

## 2. Scope and assumptions

### In scope

- Ingest a single locally accessible text file by specified path.
- Chunk plain-text content and persist vectors plus source metadata in Qdrant.
- Replace a file's existing indexed chunks when it is re-ingested.
- Search for the top `N` chunks semantically similar to a text query.
- Run Qdrant locally through the existing Docker Compose configuration.
- Provide unit, Infrastructure, and HTTP integration coverage.

### Out of scope

- Uploading files through this vector API; the existing file API remains
  responsible for uploads.
- OCR, image, audio, and scanned-PDF extraction.
- Authentication, authorization, multi-tenancy, background queues, and
  indexing a directory tree.
- Hybrid lexical/vector search, reranking, and a user interface.

### Assumptions and decisions

| Topic | Decision |
|---|---|
| Initial supported formats | UTF-8/UTF-16 text files (`.txt`, `.md`, `.json`, `.csv`, `.log`, `.cs`, and other configured text extensions). Unsupported extensions return a validation error. |
| Source path access | The path must resolve beneath a configured allowed root. Absolute paths are permitted only when they are inside that root. This prevents the API from becoming an arbitrary local-file reader. |
| Embedding model | `BAAI/bge-small-en-v1.5`, downloaded once and executed locally from ONNX artifacts. It is free under Apache-2.0, produces 384-dimensional vectors, and offers substantially better English retrieval quality than basic embedding baselines. |
| Language | English-first retrieval. A later multilingual requirement should add a separately versioned model and collection migration. |
| Vector distance | Cosine similarity, which matches the model's normalized semantic embedding use case. |
| Vector database index | Callers must provide a `vectorIndexName` for both ingest and search. It maps one-to-one to a Qdrant collection and isolates search and re-ingestion within that collection. |
| Re-ingestion | Re-ingest is replace-by-source **within the specified vector index**: all existing chunks for the canonical source path in that index are deleted, then the newly generated chunks are inserted. The response reports the index, new document version, and chunk count. |
| Failure atomicity | New chunks are upserted with a new document version before the old version is removed. Searches only return the active version recorded in a small local document catalog. If the operation fails, the old active version remains searchable. |

## 3. Architecture

### Dependency graph

```text
HiveMind.Web
    -> HiveMind.Core
    -> HiveMind.Infrastructure (composition root and concrete adapters only)

HiveMind.Infrastructure -> HiveMind.Core
HiveMind.Core           -> no project or NuGet dependency
```

`HiveMind.Core` must not reference Qdrant, ONNX Runtime, ASP.NET, or
`System.IO`. Its services accept one command or query object and return
`AppResult` / `AppResult<T>`, consistent with the existing file use cases.

### Request flows

#### Ingest

```text
POST /api/vector-documents/ingest
  -> VectorDocumentsController
  -> IVectorDocumentService.IngestAsync(IngestVectorDocumentCommand)
  -> ISourceTextReader (Infrastructure file adapter)
  -> ITextChunker (Core)
  -> ITextEmbeddingProvider (Infrastructure ONNX adapter)
  -> IVectorDocumentStore (Infrastructure Qdrant adapter)
  -> IngestVectorDocumentResponse
```

The service validates the index name and command, resolves and reads the
allowed source, calculates a SHA-256 content hash, chunks the text, creates
embeddings in batches, creates or validates the requested collection, upserts
the new document version, marks it active, and removes superseded points in
that index only.

#### Search

```text
POST /api/vector-documents/search
  -> VectorDocumentsController
  -> IVectorDocumentService.SearchAsync(SearchVectorDocumentsQuery)
  -> ITextEmbeddingProvider (Infrastructure ONNX adapter)
  -> IVectorDocumentStore (Infrastructure Qdrant adapter)
  -> SearchVectorDocumentsResponse
```

The service validates the index name, query, and requested result count,
embeds the query, and asks the requested Qdrant collection for nearest active
chunks. The controller does no chunking, embedding, or Qdrant work.

## 4. Proposed solution layout

```text
src/
  HiveMind.Core/
    VectorSearch/
      Commands.cs
      Queries.cs
      VectorDocumentModels.cs
      VectorSearchValidationRules.cs
      Interfaces.cs
      TextChunker.cs
      VectorDocumentService.cs
  HiveMind.Infrastructure/
    VectorSearch/
      FileSystemSourceTextReader.cs
      OnnxTextEmbeddingProvider.cs
      QdrantVectorDocumentStore.cs
      VectorSearchOptions.cs
      QdrantOptions.cs
      ModelAssetManager.cs
    DependencyInjection.cs
  HiveMind.Web/
    Controllers/
      VectorDocumentsController.cs
    Contracts/
      VectorSearchRequests.cs
      VectorSearchResponses.cs
    Program.cs
    appsettings.json
tests/
  HiveMind.Core.UnitTests/
    VectorSearch/
  HiveMind.Infrastructure.Tests/
    VectorSearch/
  HiveMind.Web.IntegrationTests/
    VectorSearch/
docker-compose/
  qdrant/docker-compose.yaml
```

## 5. Core design

### 5.1 Commands, query, and outputs

```csharp
public sealed record IngestVectorDocumentCommand(
    string VectorIndexName,
    string SourcePath);

public sealed record SearchVectorDocumentsQuery(
    string VectorIndexName,
    string QueryText,
    int Take = 5);

public sealed record IngestedVectorDocument(
    string VectorIndexName,
    string SourceName,
    string CanonicalSourcePath,
    string ContentHash,
    string DocumentVersion,
    int ChunkCount);

public sealed record VectorSearchHit(
    string VectorIndexName,
    string SourceName,
    string CanonicalSourcePath,
    int ChunkIndex,
    int StartCharacter,
    int EndCharacter,
    string Text,
    float Score);
```

`VectorIndexName` is required for both operations and must match
`^[a-z0-9][a-z0-9_-]{0,62}$`; Core rejects invalid names before any external
operation. This deliberately permits only a safe Qdrant collection-name subset,
avoids injection into storage operations, and leaves a future reserved naming
prefix available. `Take` defaults to 5 and must be within 1–50. Query text
must be non-empty after trimming and limited to the model's configured maximum
input tokens. `SourcePath` must be non-empty and is validated by the
Infrastructure source reader after canonicalization, because only it knows the
configured root.

### 5.2 Core interfaces

```csharp
public interface IVectorDocumentService
{
    Task<AppResult<IngestedVectorDocument>> IngestAsync(
        IngestVectorDocumentCommand command, CancellationToken ct);

    Task<AppResult<IReadOnlyList<VectorSearchHit>>> SearchAsync(
        SearchVectorDocumentsQuery query, CancellationToken ct);
}

public interface ISourceTextReader
{
    Task<AppResult<SourceText>> ReadAsync(string sourcePath, CancellationToken ct);
}

public interface ITextEmbeddingProvider
{
    int VectorSize { get; }
    Task<IReadOnlyList<ReadOnlyMemory<float>>> EmbedAsync(
        IReadOnlyList<string> texts, CancellationToken ct);
}

public interface IVectorDocumentStore
{
    Task EnsureCollectionAsync(string vectorIndexName, int vectorSize, CancellationToken ct);
    Task UpsertVersionAsync(string vectorIndexName, VectorDocumentVersion version,
        IReadOnlyList<VectorChunk> chunks,
        IReadOnlyList<ReadOnlyMemory<float>> vectors, CancellationToken ct);
    Task ActivateVersionAsync(string vectorIndexName, string canonicalSourcePath, string version, CancellationToken ct);
    Task DeleteInactiveVersionsAsync(string vectorIndexName, string canonicalSourcePath, string activeVersion, CancellationToken ct);
    Task<IReadOnlyList<VectorSearchHit>> SearchAsync(
        string vectorIndexName, ReadOnlyMemory<float> queryVector, int take, CancellationToken ct);
}
```

`SourceText`, `VectorChunk`, and `VectorDocumentVersion` are Core records. The
store interface deliberately accepts domain records rather than Qdrant DTOs.
Expected errors, such as a missing file, invalid extension, or invalid input,
are returned as `AppResult` failures. Unexpected file, model, or Qdrant
failures are logged by Infrastructure and allowed to reach the existing global
exception handler as a problem response.

### 5.3 Chunking rules

`TextChunker` is a deterministic, pure Core service and receives
`ChunkSizeTokens` and `ChunkOverlapTokens` through
`VectorSearchValidationRules`.

1. Normalize line endings to `\n`, remove a leading BOM, and preserve all
   otherwise meaningful source content.
2. Prefer paragraph and line boundaries; use sentence boundaries only when a
   paragraph exceeds the chunk limit; split an overlong token as a last resort.
3. Produce chunks of at most **400 estimated model tokens**, with a
   **60-token overlap**. The Infrastructure tokenizer performs a final exact
   token-limit guard before embedding.
4. Drop whitespace-only chunks and preserve zero-based `ChunkIndex`,
   `StartCharacter`, and exclusive `EndCharacter` positions in normalized
   text.
5. Prefix the text passed to the BGE model with its recommended retrieval
   instruction for queries only:
   `Represent this sentence for searching relevant passages: `. Document chunks
   are embedded without a prefix.

The API returns the stored chunk text exactly as indexed, along with source
name and offsets. This makes a result independently useful and traceable back
to the original file.

## 6. Infrastructure design

### 6.1 Local embedding

`OnnxTextEmbeddingProvider` loads the ONNX model and tokenizer from
`VectorSearch:ModelDirectory`; it never calls a hosted embedding API.

- Required packages: `Microsoft.ML.OnnxRuntime` and a compatible local
  Hugging Face tokenizer package. Keep these package references exclusively in
  `HiveMind.Infrastructure`.
- `ModelAssetManager` explicitly downloads or verifies pinned model artifacts
  during an installation/bootstrap step, verifies a published SHA-256 checksum,
  and records the model revision. The running API fails startup validation with
  a clear message if artifacts are missing; it must not download models while
  serving a request.
- Use CPU inference initially with a bounded batch size of 32. Make execution
  provider and batch size configuration options so CUDA can be enabled later
  without changing Core.
- Mean-pool the model's final hidden state using the attention mask, then L2
  normalize each vector. Validate that every output has exactly 384 finite
  float values before persistence.

### 6.2 Qdrant collection and point schema

Use the official `Qdrant.Client` package only in Infrastructure. On ingest,
`EnsureCollectionAsync` creates the requested index's collection if absent
with 384-dimensional `Cosine` vectors. If it exists with another dimension or
distance metric, return a clear failure rather than silently mixing
incompatible embeddings. Search returns a validation failure when its requested
collection does not exist; it never creates an empty collection.

`vectorIndexName` is the Qdrant collection name, supplied by the caller. Names
should include an application-owned schema/version suffix, for example
`engineering_docs_v1`. A changed model, vector dimension, or incompatible
chunking scheme requires callers to use a new index name and complete a
controlled reindex.

Each Qdrant point contains:

| Field | Qdrant type | Purpose |
|---|---|---|
| Point ID | deterministic UUID | UUIDv5 of `canonicalSourcePath + documentVersion + chunkIndex`; retry-safe upsert |
| vector | 384 float vector | Local model embedding |
| `sourcePath` | keyword payload | Canonical absolute path under the allowed root; filter/delete key |
| `sourceName` | keyword payload | Original file name returned to callers |
| `contentHash` | keyword payload | SHA-256 hash that identifies source bytes |
| `documentVersion` | keyword payload | Version created for this ingestion |
| `isActive` | bool payload | Search filter to expose one successful version per source |
| `chunkIndex` | integer payload | Original chunk order |
| `startCharacter` | integer payload | Inclusive normalized-text offset |
| `endCharacter` | integer payload | Exclusive normalized-text offset |
| `text` | text payload | The chunk returned in the search result |
| `modelRevision` | keyword payload | Auditable embedding model revision |

Create payload indexes for `sourcePath`, `documentVersion`, and `isActive`.
Search uses an `isActive = true` filter, `limit = Take`, and `withPayload =
true`; it does not expose raw vectors.

### 6.3 Version activation and cleanup

Qdrant filtering alone cannot atomically choose one document version across
many points. `QdrantVectorDocumentStore` therefore maintains a small local
JSON catalog in the configured application-data directory using atomic
replace writes. It maps the pair of `vectorIndexName` and
`canonicalSourcePath` to its currently active `documentVersion`.

Ingestion procedure:

1. Read and hash the source. If its hash matches the catalog's active version
   for the requested index, return success with its existing chunk count
   without recomputing embeddings.
2. Generate a new opaque version ID and upsert all inactive points for it.
3. Verify Qdrant reports the expected point count for that source/version.
4. Atomically update the catalog to select the new version, then set its
   payload `isActive = true`.
5. Set older versions for the same source in the same index inactive and
   delete them by payload filter.

If steps 1–3 fail, the catalog and prior active version are unchanged. If a
failure happens after catalog activation, startup reconciliation makes catalog
state authoritative and repairs the `isActive` payload before accepting
requests. A process-wide keyed lock on `(vectorIndexName, canonicalSourcePath)`
prevents two concurrent ingestions of the same file into the same index from
racing.

## 7. HTTP API

### `POST /api/vector-documents/ingest`

Request:

```json
{
  "vectorIndexName": "architecture_docs_v1",
  "sourcePath": "/workspace/data/architecture.md"
}
```

Success (`200 OK`):

```json
{
  "vectorIndexName": "architecture_docs_v1",
  "sourceName": "architecture.md",
  "canonicalSourcePath": "/workspace/data/architecture.md",
  "contentHash": "sha256:...",
  "documentVersion": "01J...",
  "chunkCount": 18
}
```

`400` is returned for a missing or invalid `vectorIndexName`, an empty path,
unsupported extension, or a path outside the allowed root. `404` is returned
when the permitted path does not exist. `413` is returned when the source
exceeds `MaxSourceFileSizeBytes`. Qdrant or model failures are logged and use
the existing `500` problem-details response.

### `POST /api/vector-documents/search`

Request:

```json
{
  "vectorIndexName": "architecture_docs_v1",
  "queryText": "How do project dependencies point inward?",
  "take": 3
}
```

Success (`200 OK`):

```json
{
  "results": [
    {
      "vectorIndexName": "architecture_docs_v1",
      "sourceName": "cleanArchitecture.md",
      "canonicalSourcePath": "/workspace/specs/cleanArchitecture.md",
      "chunkIndex": 2,
      "startCharacter": 782,
      "endCharacter": 1168,
      "text": "Dependencies point inward towards the Core project...",
      "score": 0.91
    }
  ]
}
```

`400` is returned for a missing or invalid `vectorIndexName`, blank query text,
or `take` outside 1–50. `404` is returned when the requested index does not
exist. An empty, valid search returns `200` with `"results": []`.

Both endpoints accept a request cancellation token, publish OpenAPI XML
documentation, and use the existing `AppResult`-to-HTTP mapping extension.

## 8. Configuration and local operations

Add these settings to `appsettings.json`; production values should be supplied
through environment variables or user secrets, not committed secrets.

```json
{
  "VectorSearch": {
    "AllowedSourceRoot": "./data/files",
    "MaxSourceFileSizeBytes": 10485760,
    "SupportedExtensions": [ ".txt", ".md", ".json", ".csv", ".log", ".cs" ],
    "ChunkSizeTokens": 400,
    "ChunkOverlapTokens": 60,
    "EmbeddingBatchSize": 32,
    "ModelDirectory": "../../models/bge-small-en-v1.5-onnx/onnx",
    "ModelRevision": "pinned-model-revision",
    "Qdrant": {
      "Endpoint": "http://localhost:6334",
      "ApiKey": ""
    }
  }
}
```

Validate these options at startup: existing allowed root and model directory,
positive maximum source size and batch size, chunk size within the model limit,
overlap smaller than chunk size, and valid Qdrant endpoint. Index names are
validated per request, rather than configured globally. Register `QdrantClient`
and the embedding provider as singletons; register `VectorDocumentService` as
scoped.

The existing [docker-compose.yaml](../docker-compose/qdrant/docker-compose.yaml)
starts Qdrant with REST on `6333` and gRPC on `6334`. Replace the committed
placeholder API key with `${QDRANT_API_KEY:?Set QDRANT_API_KEY}` and document
an example in `.env.example`; do not store a real key in source control.

Local startup:

```bash
export QDRANT_API_KEY=local-development-secret
docker compose -f docker-compose/qdrant/docker-compose.yaml up -d
dotnet run --project src/HiveMind.Web
```

## 9. Testing strategy

| Layer | Coverage |
|---|---|
| Core unit tests | Chunk boundaries, overlap, offsets, whitespace removal, command/query validation including safe index names, embedding batch orchestration, idempotent hash path per index, result mapping, and propagation of expected `AppResult` failures. Mock Core interfaces only. |
| Infrastructure tests | Temporary allowed-root fixtures; path escape and missing-file behavior; encoding support; ONNX vector dimensions/normalization using pinned test model; Qdrant per-index collection validation, payload mapping, index-isolated upsert/filter/search mapping, and version cleanup. Run Qdrant cases against Docker Compose or Testcontainers. |
| Web integration tests | Request validation and status codes, required index name response contract/OpenAPI visibility, ingest then search in the specified index using test implementations, index isolation, cancellation forwarding, and problem-details behavior for unexpected adapter failures. |
| Manual smoke test | Start local Qdrant, ingest [cleanArchitecture.md](./cleanArchitecture.md) into `architecture_docs_v1`, then search that index for `dependencies point inward`; assert one of the top results names `cleanArchitecture.md` and contains the dependency rule. |

Tests must not make network calls to a model hub. Model assets used in tests are
checked in only if licensing and size permit; otherwise CI restores them from a
checksum-verified internal artifact cache before tests run.

## 10. Delivery sequence

1. Add Core records, abstractions, validation rules, deterministic chunker, and
   `VectorDocumentService`, with unit tests first.
2. Add validated configuration and DI registration while preserving existing
   file repository registrations.
3. Add the local source reader and catalog with filesystem integration tests.
4. Add the pinned ONNX/tokenizer bootstrap and embedding provider; verify
   dimensions, normalization, and bounded batching.
5. Add the Qdrant client adapter, collection/payload initialization, version
   activation, reconciliation, and container-backed integration tests.
6. Add controller contracts, endpoints, XML documentation, and HTTP integration
   tests.
7. Update the README with prerequisites, model bootstrap, Qdrant startup,
   configuration, endpoint examples, and local validation commands.
8. Run `dotnet build FileRepository.slnx` and
   `dotnet test FileRepository.slnx`, then execute the manual smoke test.

## 11. Acceptance criteria

- A permitted, supported local file can be ingested into a caller-specified,
  valid vector index and produces one Qdrant point per non-empty chunk, each
  containing the original source name and traceable offsets.
- Re-ingesting changed content replaces the searchable chunks for that source
  in the specified index; a failed re-ingestion leaves the last successful
  version in that index searchable.
- A search query includes a valid vector index name, is embedded locally with
  the same model, searches only that index, and returns no more than the
  caller's valid requested `take` value, ranked by cosine similarity.
- Every returned result includes its source file name, canonical path, chunk
  text, offsets, index, and score.
- Core remains independent of web, Qdrant, ONNX, and file-system packages.
- Startup rejects incompatible Qdrant vector schemas and missing model assets
  with actionable errors.
- The existing file API behavior and test suite remain unchanged.

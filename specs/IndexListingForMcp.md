# Tech Plan: List Qdrant Collections (Indexes) via MCP

## Requirement
As a user, I can call an MCP tool to list the collections ("vector indexes") in my Qdrant instance.

## Current state
- MCP tools live in `src/HiveMind.Mcp/Tools/VectorSearchTools.cs` (static class, `[McpServerTool]`, delegates to Core services, returns JSON via `ToJson(AppResult<T>)`). Only `Search` and ingest-style tools exist; there is no discovery tool, so users must already know index names.
- Core: `IVectorDocumentService` / `VectorDocumentService` (validation + orchestration) and `IVectorDocumentStore` (port) in `src/HiveMind.Core/VectorSearch/Interfaces.cs`.
- Infrastructure: `QdrantVectorDocumentStore` talks to Qdrant REST through a typed `HttpClient` (base address/API key from `VectorSearchOptions`).
- Web API exposes the same use cases in `VectorDocumentsController` (optional parity).

Follows the existing Clean Architecture (`specs/cleanArchitecture.md`): MCP/Web -> Core service -> Core port -> Infrastructure adapter.

## Design
Qdrant REST: `GET /collections` -> `{ "result": { "collections": [ { "name": "..." } ] } }`.
Names only are returned there; optional enrichment per collection uses `GET /collections/{name}` (`points_count`, `config.params.vectors.size`, `distance`, `status`).

### Decisions
- Return name + details (vector size, distance, points count, status) by default? Proposed: **names plus details**, fetched in parallel (bounded, e.g. 4) — with a `includeDetails` flag (default `true`) so callers can request names only for large instances.
- Do **not** filter to this app's naming pattern (`^[a-z0-9][a-z0-9_-]{0,62}$`); list everything in the instance, and flag non-conforming names via `isCompatible` (size/distance match the embedding provider) so users know which can be used with `Search`/ingest.
- Read-only; no new auth beyond the existing Qdrant API key.

## Changes

### 1. Core (`src/HiveMind.Core/VectorSearch`)
- `VectorModels.cs`: add
  `record VectorIndexInfo(string Name, int? VectorSize, string? Distance, long? PointsCount, string? Status, bool? IsCompatible);`
- `Queries.cs`/models: add `record ListVectorIndexesQuery(bool IncludeDetails = true);`
- `Interfaces.cs`:
  - `IVectorDocumentStore.ListIndexesAsync(bool includeDetails, int expectedVectorSize, CancellationToken ct)` -> `AppResult<IReadOnlyList<VectorIndexInfo>>`
  - `IVectorDocumentService.ListIndexesAsync(ListVectorIndexesQuery, CancellationToken)`
- `VectorDocumentService.cs`: implement by passing `embeddingProvider.VectorSize` to the store; return sorted by name.

### 2. Infrastructure (`QdrantVectorDocumentStore.cs`)
- `GET /collections`, parse `result.collections[].name`.
- If `includeDetails`, `GET /collections/{name}` per collection with `SemaphoreSlim`/`Parallel.ForEachAsync` (max 4); tolerate 404 (collection deleted mid-listing -> skip) and handle missing/named-vector configs (`vectors` without `size` -> null size, `IsCompatible=false`).
- Reuse `EnsureSuccessAsync`; empty instance returns `Ok([])`. Connection failures propagate to `GlobalExceptionHandler`/MCP error as with Search.
- Percent-encode names in URLs (`Uri.EscapeDataString`).

### 3. MCP (`VectorSearchTools.cs`)
```csharp
[McpServerTool, Description("Lists the vector indexes (Qdrant collections) available in the Qdrant instance, with vector size, distance metric, point count, and whether each is compatible with this server's embedding model.")]
public static async Task<string> ListIndexes(
    IVectorDocumentService svc,
    [Description("Include per-index details. Set false for names only. Defaults to true.")] bool includeDetails = true,
    CancellationToken cancellationToken = default)
    => ToJson(await svc.ListIndexesAsync(new(includeDetails), cancellationToken));
```
Tool name surfaces as `list_indexes` (follow the SDK's naming convention used by `Search`). Update the `Search` description to mention discovering names with `list_indexes`.

### 4. Web API (optional parity)
`GET /api/vector-indexes?includeDetails=true` in `VectorDocumentsController`, with contract in `VectorSearchContracts.cs`; add sample to `HiveMind.Web.http`.

### 5. Docs
README: add the tool to the MCP tools list with example request/response.

## Example response
```json
{ "success": true, "data": [
  { "name": "docs_v1", "vectorSize": 384, "distance": "Cosine", "pointsCount": 1280, "status": "green", "isCompatible": true }
]}
```

## Testing
- Unit (`tests/HiveMind.Core.UnitTests`): service passes expected vector size, sorts results, propagates store failures (fake `IVectorDocumentStore`).
- Infrastructure: `QdrantVectorDocumentStore` with a stub `HttpMessageHandler`: empty list, multiple collections, 404 on detail, non-success status, named-vector config.
- Manual/integration: `docker-compose/qdrant` up, ingest a folder via `scripts/ingest-folder.sh`, run `scripts/start-mcp-http.sh`, call `list_indexes` with an MCP client (e.g. MCP Inspector); verify against `curl localhost:6333/collections`.

## Acceptance criteria
- MCP exposes `list_indexes`; returns all collections in the configured Qdrant instance.
- `includeDetails=false` makes exactly one Qdrant request.
- Empty instance returns success with empty list; Qdrant unreachable returns a clear error.
- Build and existing tests still pass.

## Rollout / risks
- Additive change, no breaking contracts, no data migration.
- Risk: many collections -> N detail calls; mitigated by bounded parallelism and `includeDetails=false`.
- Risk: exposes names of collections not created by this app; acceptable as the same Qdrant credentials already grant that access.

## Task order
1. Core models + interfaces + service  2. Qdrant store implementation  3. MCP tool  4. Tests  5. (Optional) Web endpoint  6. README

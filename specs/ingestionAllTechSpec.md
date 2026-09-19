# Tech Plan: Ingest All Files In A Folder

## 1. Summary

Extend the vector search feature so a developer can ingest an entire folder
of files in one request instead of calling the single-file ingest endpoint
once per file. The developer supplies an input path and a vector index name;
the system iterates over the files in that folder, ingests each supported
file into the specified Qdrant collection, and reports a per-file outcome so
the caller knows exactly what was indexed, skipped, or failed.

This capability lets a team point the API at a shared folder (for example a
checked-out documentation repository or a shared drive mount) and populate a
common index that other team members can search, satisfying the "teams will
be able to share common knowledge into the vector database" story without
requiring per-file scripting.

This design follows [cleanArchitecture.md](./cleanArchitecture.md) and
extends [techPlanForVectorDb.md](./techPlanForVectorDb.md): Core adds a
directory-ingestion use case that reuses the existing single-file ingest
service; Infrastructure adds a file-system enumerator; Web adds one endpoint
and its contracts.

## 2. Scope and assumptions

### In scope

- Ingest every supported file directly inside a developer-specified folder
  path into a developer-specified vector index.
- Optionally include files in subfolders (opt-in, not recursive by default).
- Reuse the existing single-file ingest pipeline (read, chunk, embed, store)
  for each file; no new chunking, embedding, or storage logic.
- Continue past a single file's failure so one bad file does not abort the
  whole batch; report every file's outcome in the response.
- Enforce the same allowed-root and supported-extension rules as single-file
  ingest, plus a configurable maximum file count per batch.
- Provide unit, Infrastructure, and HTTP integration coverage.

### Out of scope

- Uploading a folder's bytes through this API; the folder must already be
  reachable on the server's local file system (or a mounted shared drive),
  consistent with the existing single-file `sourcePath` model.
- Parallel/concurrent file ingestion, background job queues, and progress
  polling; the initial version ingests files sequentially within one request.
- Deleting index entries for files that were removed from the folder since a
  previous ingestion run.
- Watching a folder for changes; ingestion is triggered on demand.
- A new authentication/authorization or multi-tenant model beyond what the
  existing API already has (tracked as a general gap in
  [techPlanForVectorDb.md](./techPlanForVectorDb.md)).

### Assumptions and decisions

| Topic | Decision |
|---|---|
| Input path | `directoryPath` must resolve beneath the same `VectorSearch:AllowedSourceRoot` used by single-file ingest. This keeps one security boundary for both endpoints instead of introducing a second allow-list. |
| Index name | One `vectorIndexName` applies to every file discovered in the folder for that request. Ingesting into multiple indexes from one folder requires multiple requests. |
| File discovery | The system lists entries directly in the folder by default. A `includeSubdirectories` flag (default `false`) opts into a recursive walk. Only files whose extension is in `VectorSearch:SupportedExtensions` are ingested; other files are reported as skipped, not failed. |
| Per-file isolation | Each file is ingested independently through the existing `IVectorDocumentService.IngestAsync`. A single file's validation error, missing-file race, or store failure is captured per file and does not stop the remaining files from being processed. |
| Ordering | Files are processed in ascending ordinal file-name order so results are stable and reproducible across runs. |
| Batch size limit | A configurable `MaxFilesPerDirectoryIngest` (default 500) caps how many files one request will process, to bound request duration and avoid unbounded embedding workloads. Exceeding it is a validation error naming the limit and the discovered count. |
| Re-ingestion | Re-ingesting the same folder re-runs the existing single-file replace-by-source behavior for every matching file; unchanged files are simply re-indexed with a new document version, which is acceptable for this use case. |
| Sharing knowledge | Any team member who knows the shared index name can search it once ingestion completes; this feature does not introduce a new sharing mechanism beyond the existing `vectorIndexName` convention. |

## 3. Architecture

### Dependency graph

```text
FileRepo.Web
    -> FileRepo.Core
    -> FileRepo.Infrastructure (composition root and concrete adapters only)

FileRepo.Infrastructure -> FileRepo.Core
FileRepo.Core           -> no project or NuGet dependency
```

No new external package is required. `FileRepo.Core` continues to depend on
nothing but the BCL; the new directory enumerator lives in
`FileRepo.Infrastructure` alongside `FileSystemSourceTextReader`, since only
Infrastructure knows the allowed root and touches `System.IO`.

### Request flow

```text
POST /api/vector-documents/ingest-directory
  -> VectorDocumentsController
  -> IDirectoryIngestionService.IngestDirectoryAsync(IngestVectorDocumentDirectoryCommand)
  -> IDirectoryFileEnumerator (Infrastructure file adapter)
       - resolves and validates directoryPath against AllowedSourceRoot
       - lists candidate files (optionally recursive), sorted by name
  -> for each candidate file, in order:
       IVectorDocumentService.IngestAsync(IngestVectorDocumentCommand)
         -> ISourceTextReader -> ITextChunker -> ITextEmbeddingProvider -> IVectorDocumentStore
  -> aggregate per-file outcomes into IngestedVectorDirectory
```

`DirectoryIngestionService` is a thin Core orchestrator: it does not read
files, chunk text, embed, or talk to Qdrant itself. It delegates every file to
the existing `IVectorDocumentService`, so single-file ingest behavior (hash,
versioning, replace-by-source, validation) is defined in exactly one place.

## 4. Proposed solution layout

```text
src/
  FileRepo.Core/
    VectorSearch/
      Commands.cs                     (add IngestVectorDocumentDirectoryCommand)
      VectorModels.cs                 (add IngestedVectorDirectory, DirectoryFileOutcome)
      Interfaces.cs                   (add IDirectoryIngestionService, IDirectoryFileEnumerator)
      DirectoryIngestionService.cs    (new)
  FileRepo.Infrastructure/
    VectorSearch/
      FileSystemDirectoryEnumerator.cs (new)
      VectorSearchOptions.cs           (add MaxFilesPerDirectoryIngest)
    DependencyInjection.cs             (register new service/enumerator)
  FileRepo.Web/
    Controllers/
      VectorDocumentsController.cs     (add ingest-directory endpoint)
    Contracts/
      Requests.cs / Responses.cs       (add directory request/response records)
tests/
  FileRepo.Core.UnitTests/
    VectorSearch/DirectoryIngestionServiceTests.cs
  FileRepo.Infrastructure.Tests/
    VectorSearch/FileSystemDirectoryEnumeratorTests.cs
  FileRepo.Web.IntegrationTests/
    VectorSearch/ (extend existing ingest tests with a directory case)
```

## 5. Core design

### 5.1 Command and result models

```csharp
public sealed record IngestVectorDocumentDirectoryCommand(
    string VectorIndexName,
    string DirectoryPath,
    bool IncludeSubdirectories = false);

public enum DirectoryFileIngestStatus { Ingested, Skipped, Failed }

public sealed record DirectoryFileOutcome(
    string SourcePath,
    DirectoryFileIngestStatus Status,
    IngestedVectorDocument? Document,
    string? Reason);

public sealed record IngestedVectorDirectory(
    string VectorIndexName,
    string DirectoryPath,
    int FilesDiscovered,
    int IngestedCount,
    int SkippedCount,
    int FailedCount,
    IReadOnlyList<DirectoryFileOutcome> Files);
```

`Reason` is populated for `Skipped` (for example "extension not supported")
and `Failed` (the underlying `AppResult` message or first validation error)
outcomes, and is `null` for `Ingested`. `Document` is populated only for
`Ingested` outcomes.

### 5.2 Core interfaces

```csharp
public interface IDirectoryIngestionService
{
    Task<AppResult<IngestedVectorDirectory>> IngestDirectoryAsync(
        IngestVectorDocumentDirectoryCommand command,
        CancellationToken ct);
}

public interface IDirectoryFileEnumerator
{
    Task<AppResult<IReadOnlyList<string>>> EnumerateAsync(
        string directoryPath,
        bool includeSubdirectories,
        CancellationToken ct);
}
```

`IDirectoryFileEnumerator` returns canonical, ordinal-sorted file paths. It
performs directory-level validation (existence, allowed-root containment, and
the `MaxFilesPerDirectoryIngest` limit) and returns the same `AppResult`
failure shapes (`Invalid`, `NotFound`) used elsewhere, so the controller needs
no extra mapping logic.

### 5.3 `DirectoryIngestionService` behavior

```csharp
public sealed class DirectoryIngestionService(
    IDirectoryFileEnumerator enumerator,
    IVectorDocumentService documentService) : IDirectoryIngestionService
{
    public async Task<AppResult<IngestedVectorDirectory>> IngestDirectoryAsync(
        IngestVectorDocumentDirectoryCommand command, CancellationToken ct)
    {
        // 1. Validate vectorIndexName using the same pattern as single-file ingest.
        // 2. Delegate discovery to the enumerator; propagate its failure as-is
        //    (Invalid, NotFound) if directoryPath is missing or outside the root.
        // 3. For each discovered file, in order, call
        //    documentService.IngestAsync(new(command.VectorIndexName, filePath), ct).
        // 4. Map each per-file AppResult to a DirectoryFileOutcome:
        //      Ok        -> Ingested (Document set)
        //      Invalid   -> Skipped, when the reason is an unsupported extension;
        //                   Failed otherwise (defensive; the enumerator already
        //                   filters by extension)
        //      NotFound  -> Failed (file removed between listing and reading)
        //      other     -> Failed
        // 5. Never throw for a single file's failure; continue to the next file.
        // 6. Return AppResult.Ok(IngestedVectorDirectory) with aggregated counts,
        //    even when some or all files failed, so the caller always gets a
        //    complete per-file report for a valid, existing directory.
    }
}
```

Only directory-level problems (invalid index name, invalid/missing
directory, batch size exceeded) short-circuit with a non-`Ok` `AppResult`.
Once discovery succeeds, the response is always `200 OK` with a summary,
because "some files failed" is a normal, reportable outcome rather than an
API-level error.

## 6. Infrastructure design

### 6.1 `FileSystemDirectoryEnumerator`

```csharp
public sealed class FileSystemDirectoryEnumerator(
    IOptions<VectorSearchOptions> options) : IDirectoryFileEnumerator
{
    public async Task<AppResult<IReadOnlyList<string>>> EnumerateAsync(
        string directoryPath, bool includeSubdirectories, CancellationToken ct)
    {
        // Resolve directoryPath the same way FileSystemSourceTextReader
        // resolves sourcePath: combine with AllowedSourceRoot when relative,
        // Path.GetFullPath, then require the result to be the root itself or
        // nested under it (rejecting ".." escapes and symlink traversal
        // outside the root).
        // Return NotFound if the directory does not exist.
        // Directory.EnumerateFiles(path, "*", searchOption) where
        // searchOption is TopDirectoryOnly or AllDirectories.
        // Filter to files whose extension is in SupportedExtensions;
        // non-matching files are simply excluded from the returned list
        // (the service reports them as Skipped using the full unfiltered
        // count for FilesDiscovered).
        // If the filtered count exceeds MaxFilesPerDirectoryIngest, return
        // Invalid with a message naming the limit and the discovered count.
        // Sort remaining paths with StringComparer.Ordinal and return them.
    }
}
```

Reusing `VectorSearchOptions.AllowedSourceRoot`,
`VectorSearchOptions.SupportedExtensions`, and `VectorSearchOptions.MaxSourceFileSizeBytes`
means the same configuration governs both single-file and directory ingest;
only `MaxFilesPerDirectoryIngest` is new.

To report unsupported files as `Skipped` rather than silently dropping them,
`EnumerateAsync` returns two path lists internally (all files, and supported
files) or the service computes `FilesDiscovered` from an unfiltered listing
and diffs it against the filtered/ordered list the enumerator returns;
whichever keeps `DirectoryIngestionService` simplest is acceptable as long as
unsupported files appear in the response as `Skipped` with reason
`"Extension not supported."` and are counted in `FilesDiscovered`.

### 6.2 Reused adapters

`ISourceTextReader`, `ITextChunker`, `ITextEmbeddingProvider`, and
`IVectorDocumentStore` are unchanged. Each file ingested from a directory goes
through the exact same `FileSystemSourceTextReader` allowed-root check,
extension check, size check, and encoding detection as single-file ingest, so
no security or validation rule is duplicated or weakened for the batch path.

### 6.3 Configuration addition

```csharp
public sealed class VectorSearchOptions
{
    // ...existing members unchanged...
    [Range(1, 100_000)] public int MaxFilesPerDirectoryIngest { get; set; } = 500;
}
```

Registered the same way as the rest of `VectorSearchOptions`, via
`AddOptions<VectorSearchOptions>().BindConfiguration("VectorSearch")`, so no
new options section is introduced.

## 7. HTTP API

### `POST /api/vector-documents/ingest-directory`

Request:

```json
{
  "vectorIndexName": "engineering_docs_v1",
  "directoryPath": "/workspace/data/engineering-docs",
  "includeSubdirectories": true
}
```

Success (`200 OK`):

```json
{
  "vectorIndexName": "engineering_docs_v1",
  "directoryPath": "/workspace/data/engineering-docs",
  "filesDiscovered": 12,
  "ingestedCount": 10,
  "skippedCount": 1,
  "failedCount": 1,
  "files": [
    {
      "sourcePath": "/workspace/data/engineering-docs/architecture.md",
      "status": "Ingested",
      "document": {
        "vectorIndexName": "engineering_docs_v1",
        "sourceName": "architecture.md",
        "canonicalSourcePath": "/workspace/data/engineering-docs/architecture.md",
        "contentHash": "sha256:...",
        "documentVersion": "01J...",
        "chunkCount": 18
      },
      "reason": null
    },
    {
      "sourcePath": "/workspace/data/engineering-docs/diagram.png",
      "status": "Skipped",
      "document": null,
      "reason": "Extension not supported."
    },
    {
      "sourcePath": "/workspace/data/engineering-docs/locked-file.txt",
      "status": "Failed",
      "document": null,
      "reason": "Source file exceeds the maximum size of 10485760 bytes."
    }
  ]
}
```

`400` is returned for a missing or invalid `vectorIndexName`, an empty
`directoryPath`, a path outside the allowed root, or a discovered file count
above `MaxFilesPerDirectoryIngest`. `404` is returned when the permitted
directory path does not exist. Per-file problems never change the endpoint's
own status code; they are reported inside the `200` response body as
described above. Unexpected model or Qdrant failures during a specific file's
ingestion are captured as that file's `Failed` outcome with a generic reason
and are also logged with full detail by Infrastructure; they do not abort the
remaining files and do not produce a `500` unless they occur outside the
per-file loop (for example, during directory validation itself).

The endpoint accepts a request cancellation token and publishes OpenAPI XML
documentation, consistent with the existing single-file endpoints.

## 8. Configuration and local operations

No new configuration section is required; add one field to the existing
`VectorSearch` block:

```json
{
  "VectorSearch": {
    "MaxFilesPerDirectoryIngest": 500
  }
}
```

Validate `MaxFilesPerDirectoryIngest` as a positive integer at startup,
alongside the existing `VectorSearchOptions` validation. Local operation is
unchanged: start Qdrant, then call the folder path once instead of scripting
repeated calls to `/api/vector-documents/ingest`:

```bash
export QDRANT_API_KEY=local-development-secret
docker compose -f docker-compose/qdrant/docker-compose.yaml up -d
dotnet run --project src/FileRepo.Web
curl -X POST http://localhost:5000/api/vector-documents/ingest-directory \
  -H "Content-Type: application/json" \
  -d '{"vectorIndexName":"engineering_docs_v1","directoryPath":"./data/files/engineering-docs","includeSubdirectories":true}'
```

## 9. Testing strategy

| Layer | Coverage |
|---|---|
| Core unit tests | Command/index-name validation, ordering (ordinal by file name), per-file outcome mapping (`Ok`→`Ingested`, unsupported extension→`Skipped`, `NotFound`/store failure→`Failed`), aggregated counts, continuation past a failing file, and propagation of enumerator-level `Invalid`/`NotFound` failures without calling `IVectorDocumentService`. Mock `IDirectoryFileEnumerator` and `IVectorDocumentService`. |
| Infrastructure tests | Temporary allowed-root fixtures with a mix of supported/unsupported files and nested subfolders; top-level vs. `includeSubdirectories` behavior; path escape and missing-directory behavior; ordinal sort order; `MaxFilesPerDirectoryIngest` boundary (at limit succeeds, over limit fails with the discovered count in the message). |
| Web integration tests | Request validation and status codes for directory-level errors (`400`/`404`); a full ingest-directory-then-search flow against a fixture folder with at least one supported, one unsupported, and one oversized file, asserting the response reports `Ingested`, `Skipped`, and `Failed` respectively; cancellation forwarding. |
| Manual smoke test | Ingest the [specs/](./) folder itself (`.md` files) into `specs_docs_v1` with `includeSubdirectories: false`, confirm `filesDiscovered` matches the folder's Markdown file count, then search `specs_docs_v1` for a phrase known to exist in one of those files and confirm it is returned. |

Existing single-file ingest and search tests are unaffected and continue to
run unchanged, since `DirectoryIngestionService` only calls the existing
`IVectorDocumentService` abstraction rather than modifying it.

## 10. Delivery sequence

1. Add Core records (`IngestVectorDocumentDirectoryCommand`,
   `DirectoryFileOutcome`, `IngestedVectorDirectory`), the
   `IDirectoryIngestionService`/`IDirectoryFileEnumerator` interfaces, and
   `DirectoryIngestionService`, with unit tests first using fakes for both
   dependencies.
2. Add `MaxFilesPerDirectoryIngest` to `VectorSearchOptions` and its
   validation.
3. Add `FileSystemDirectoryEnumerator` with filesystem integration tests
   covering allowed-root containment, recursion toggle, sorting, and the
   batch-size limit.
4. Register the new service and enumerator in
   `AddVectorSearchInfrastructure`.
5. Add the `ingest-directory` controller action, request/response contracts,
   and XML documentation.
6. Add HTTP integration tests covering the success, partial-failure, and
   directory-validation-error paths.
7. Update the README with the new endpoint, its request/response shape, and
   an example folder-ingestion command.
8. Run `dotnet build FileRepository.slnx` and
   `dotnet test FileRepository.slnx`, then execute the manual smoke test.

## 11. Acceptance criteria

- A developer can specify an input folder path and a vector index name in a
  single request and have every supported file in that folder ingested into
  that index.
- The response lists every discovered file with an explicit `Ingested`,
  `Skipped`, or `Failed` status, plus aggregated counts, so the caller can
  confirm what was indexed without inspecting server logs.
- A single unreadable, oversized, or invalid file does not prevent the other
  files in the folder from being ingested.
- `includeSubdirectories` correctly toggles between top-level-only and
  recursive discovery.
- The folder path is subject to the same allowed-root containment as
  single-file ingest; paths outside the configured root are rejected.
- A folder containing more files than `MaxFilesPerDirectoryIngest` is
  rejected up front with a clear validation error instead of a partial run.
- Once ingested, any team member can run the existing search endpoint
  against the shared `vectorIndexName` and retrieve chunks from any of the
  ingested files.
- The existing single-file ingest and search endpoints, their validation
  rules, and their test suites remain unchanged.

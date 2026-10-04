# Tech Plan: File Repository Web API

### standards
- Follow specs/cleanArchitecture.md

## 1. Summary

A .NET Web API that lets a user **add, update, delete, get, and list** files in a file repository. The repository is a folder on disk, and its location comes from `appsettings.json`. The solution follows the Clean Architecture rules in `specs/cleanArchitecture.md` (Ardalis/Smith style): Core, Infrastructure, Web.

### Assumptions

- I couldn't read `specs/cleanArchitecture.md` from the workspace. I used the copy you pasted earlier in the "RAG data ingestion example with Python" chat. If the file has since changed, re-check section 3.
- **Flat repository**: files live directly in the root folder, with no subfolders. Nested paths would add path-traversal surface and need extra design.
- **Add** fails if the file already exists. **Update** fails if it doesn't. Neither silently overwrites or upserts.
- **Auth is out of scope** for this iteration.
- Target **.NET 10 (LTS)**. Everything here also works on .NET 8.

---

## 2. User Stories and Acceptance Criteria

| # | Story | Acceptance criteria |
|---|---|---|
| 1 | As a user, I can **add** a file to the repository | Uploading a new valid file returns `201` with file info. An existing name returns `409`. An invalid name or oversize file returns `400` with validation errors. The bytes on disk match the upload. |
| 2 | As a user, I can **update** a file in the repository | Uploading to an existing name replaces its content and returns `200`. A missing file returns `404`. Replacement is atomic, so a failed write never leaves a half-written file. |
| 3 | As a user, I can **delete** a file from the repository | Deleting an existing file returns `204`. A missing file returns `404`. |
| 4 | As a user, I can **get** a file | An existing file returns `200` with the file bytes and a content type. A missing file returns `404`. |
| 5 | As a user, I can **list** files in the repository | Returns `200` with a page of file summaries (name, size, timestamps) sorted by name, plus the total count. Supports an optional name prefix, `skip`, and `take`. |

---

## 3. Clean Architecture Rules Applied

These rules come from the spec and drive the design below.

| Spec rule | How this plan applies it |
|---|---|
| Dependencies point inward | `Core` references nothing. `Infrastructure` references `Core`. `Web` references `Core` and `Infrastructure`, the latter only for DI registration in the composition root. |
| Core has minimal framework dependencies | `HiveMind.Core` has **zero NuGet packages**, no `Microsoft.Extensions.*`, and no FluentValidation. Validation is hand-written. |
| Inner defines interfaces, outer implements | `IFileRepository` lives in Core. `FileSystemFileRepository` lives in Infrastructure. |
| Infra owns file system access | All `System.IO` calls against the repo folder are in Infrastructure. |
| Web has no business logic | Controllers only bind requests, call `IFileService`, and map `AppResult` to HTTP. |
| Core services depend on repositories/providers via interfaces | `FileService` takes `IFileRepository` and nothing concrete. |
| Core services take command or query objects | Every `IFileService` method takes exactly one command or query record. |
| Core services return an app result | Every `IFileService` method returns `AppResult` or `AppResult<T>` with status, messages, and validation errors. Expected failures never throw. |
| Core services have unit tests | `HiveMind.Core.UnitTests` covers every service path with a mocked `IFileRepository`. |

### Dependency graph

```
        ┌───────────────┐
        │  HiveMind.Web │  controllers, Swagger, Program.cs (composition root)
        └───┬───────┬───┘
            │       │
            │       ▼
            │  ┌──────────────────────┐
            │  │ HiveMind.Infrastructure │  FileSystemFileRepository
            │  └──────────┬───────────┘
            ▼             ▼
        ┌──────────────────────┐
        │     HiveMind.Core    │  entities, interfaces, commands/queries,
        │  (no dependencies)   │  AppResult, FileService, validation
        └──────────────────────┘
```

---

## 4. Tech Stack

| Concern | Choice |
|---|---|
| Language / runtime | C# 14, .NET 10 (LTS) |
| Web | ASP.NET Core Web API **with controllers** (not minimal APIs) |
| API docs | **Swashbuckle.AspNetCore**: OpenAPI document plus Swagger UI at `/swagger` |
| Configuration | `appsettings.json` plus the options pattern, validated on startup |
| Tests | **NUnit**, `NUnit3TestAdapter`, `Microsoft.NET.Test.Sdk` |
| Mocking | NSubstitute |
| Integration tests | `Microsoft.AspNetCore.Mvc.Testing` (`WebApplicationFactory`) |

**Swagger alternative:** Microsoft's built-in `Microsoft.AspNetCore.OpenApi` generates the OpenAPI document, and Swagger UI or Scalar can be layered on top. I chose Swashbuckle because it is the most direct match for "swagger" and handles multipart uploads without extra work. Swapping later only touches `Web`.

---

## 5. Solution Layout

```
FileRepository.sln
src/
  HiveMind.Core/
    Common/
      AppResult.cs                 AppResult, AppResult<T>, ResultStatus, ValidationError
      PagedResult.cs
    Files/
      FileSummary.cs               Name, SizeBytes, CreatedUtc, ModifiedUtc
      FileContent.cs               FileSummary + readable Stream
      FileValidationRules.cs       plain POCO: MaxFileSizeBytes, MaxNameLength
      Commands/
        AddFileCommand.cs
        UpdateFileCommand.cs
        DeleteFileCommand.cs
      Queries/
        GetFileQuery.cs
        ListFilesQuery.cs
      Interfaces/
        IFileRepository.cs
        IFileService.cs
      FileService.cs
      FileNameValidator.cs
  HiveMind.Infrastructure/
    FileSystem/
      FileSystemFileRepository.cs
      FileSystemRepositoryOptions.cs   RootPath
    DependencyInjection.cs             AddFileRepositoryInfrastructure(config)
  HiveMind.Web/
    Controllers/
      FilesController.cs
    Contracts/                         request/response DTOs
      AddFileRequest.cs
      UpdateFileRequest.cs
      DeleteFileRequest.cs
      GetFileRequest.cs
      ListFilesRequest.cs
      FileSummaryResponse.cs
      ListFilesResponse.cs
    Extensions/
      AppResultExtensions.cs           AppResult -> IActionResult
    Program.cs
    appsettings.json
    appsettings.Development.json
tests/
  HiveMind.Core.UnitTests/
  HiveMind.Infrastructure.Tests/
  HiveMind.Web.IntegrationTests/
```

---

## 6. Core Design

### 6.1 AppResult

```csharp
public enum ResultStatus { Ok, Invalid, NotFound, Conflict, Error }

public sealed record ValidationError(string Field, string Message);

public class AppResult
{
    public ResultStatus Status { get; }
    public bool IsSuccess => Status == ResultStatus.Ok;
    public IReadOnlyList<string> Messages { get; }
    public IReadOnlyList<ValidationError> ValidationErrors { get; }

    public static AppResult Ok();
    public static AppResult Invalid(IEnumerable<ValidationError> errors);
    public static AppResult NotFound(string message);
    public static AppResult Conflict(string message);
    public static AppResult Error(string message);
}

public sealed class AppResult<T> : AppResult
{
    public T? Value { get; }
    public static AppResult<T> Ok(T value);
    // plus the same Invalid / NotFound / Conflict / Error factories
}
```

### 6.2 Commands and queries

```csharp
public sealed record AddFileCommand(string FileName, Stream Content, long SizeBytes);
public sealed record UpdateFileCommand(string FileName, Stream Content, long SizeBytes);
public sealed record DeleteFileCommand(string FileName);
public sealed record GetFileQuery(string FileName);
public sealed record ListFilesQuery(string? NamePrefix = null, int Skip = 0, int Take = 100);
```

### 6.3 Outputs

```csharp
public sealed record FileSummary(string Name, long SizeBytes, DateTime CreatedUtc, DateTime ModifiedUtc);
public sealed record FileContent(FileSummary Info, Stream Content);   // caller disposes Content
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount);
```

### 6.4 Interfaces

The repository methods use `Try*` semantics. Expected outcomes such as "already exists" or "not found" come back as `null` or `false`, not exceptions. This is also race-safe: the repository decides atomically instead of the service doing check-then-act.

```csharp
public interface IFileRepository
{
    Task<FileSummary?> TryCreateAsync(string name, Stream content, CancellationToken ct);  // null = already exists
    Task<FileSummary?> TryReplaceAsync(string name, Stream content, CancellationToken ct); // null = not found
    Task<bool>         TryDeleteAsync(string name, CancellationToken ct);                  // false = not found
    Task<FileContent?> TryOpenReadAsync(string name, CancellationToken ct);                // null = not found
    Task<PagedResult<FileSummary>> ListAsync(string? namePrefix, int skip, int take, CancellationToken ct);
}

public interface IFileService
{
    Task<AppResult<FileSummary>>            AddAsync(AddFileCommand command, CancellationToken ct);
    Task<AppResult<FileSummary>>            UpdateAsync(UpdateFileCommand command, CancellationToken ct);
    Task<AppResult>                         DeleteAsync(DeleteFileCommand command, CancellationToken ct);
    Task<AppResult<FileContent>>            GetAsync(GetFileQuery query, CancellationToken ct);
    Task<AppResult<PagedResult<FileSummary>>> ListAsync(ListFilesQuery query, CancellationToken ct);
}
```

### 6.5 Validation (in Core, hand-written)

`FileNameValidator` rejects:

- empty or whitespace names;
- names longer than `MaxNameLength` (default 255);
- any path separator (`/`, `\`), or a name that differs from `Path.GetFileName(name)`;
- `.` and `..`;
- invalid file name characters (`Path.GetInvalidFileNameChars()`);
- names starting with `.` (this protects the internal staging folder and hidden files);
- trailing dots or spaces (Windows edge case).

Other rules:

- Add and update reject `SizeBytes <= 0` or `> MaxFileSizeBytes`.
- List rejects `Skip < 0`, `Take < 1`, and `Take > 1000`.
- `FileValidationRules` is a plain POCO registered as a singleton by the composition root, so Core needs no `IOptions` dependency.

### 6.6 FileService behavior

| Method | Flow |
|---|---|
| `AddAsync` | Validate, then `TryCreateAsync`. `null` gives `Conflict`. Otherwise `Ok(summary)`. |
| `UpdateAsync` | Validate, then `TryReplaceAsync`. `null` gives `NotFound`. Otherwise `Ok(summary)`. |
| `DeleteAsync` | Validate name, then `TryDeleteAsync`. `false` gives `NotFound`. Otherwise `Ok`. |
| `GetAsync` | Validate name, then `TryOpenReadAsync`. `null` gives `NotFound`. Otherwise `Ok(content)`. |
| `ListAsync` | Validate paging, then `ListAsync`. Always `Ok`. |

Unexpected exceptions from the repository are **not** caught in Core. They bubble to the Web layer's global exception handler (section 8.4).

---

## 7. Infrastructure Design

### 7.1 Configuration

`appsettings.json`:

```json
{
  "FileRepository": {
    "RootPath": "./data/files",
    "MaxFileSizeBytes": 10485760
  }
}
```

- `FileSystemRepositoryOptions { string RootPath }` lives in Infrastructure. It is bound with `AddOptions<T>().BindConfiguration("FileRepository").ValidateDataAnnotations().ValidateOnStart()`, so a missing `RootPath` fails at startup instead of on the first request.
- `FileValidationRules` in Core is bound from the same section by `Web`'s composition root.
- Environments override with `FileRepository__RootPath`. Tests override with `WebApplicationFactory`.
- On startup, the repository creates `RootPath` if it doesn't exist.

### 7.2 FileSystemFileRepository

- **Safe path resolution.** `Path.GetFullPath(Path.Combine(root, name))` must start with the root plus a separator. This is a second line of defence behind the Core validator, since Infrastructure shouldn't trust its caller with security.
- **Atomic writes.** Stream the upload to `{root}/.staging/{guid}.tmp`, then `File.Move` into place.
  - Create uses `overwrite: false`. An `IOException` where the destination now exists maps to "already exists" (`null`).
  - Replace checks that the destination exists, then moves with `overwrite: true`. If it is missing, the temp file is deleted and `null` is returned.
  - A `finally` block always cleans up the temp file.
- **Streaming.** Never buffer whole files in memory. Copy with `CopyToAsync` and the cancellation token.
- **Get.** Open with `FileShare.Read`, `FileOptions.Asynchronous | SequentialScan`. Return the stream and let the caller dispose it.
- **List.** Enumerate top-level files only (so `.staging/` is naturally excluded), filter by ordinal prefix, sort by ordinal name, then apply skip and take. Timestamps come from `FileInfo` in UTC.
- **Delete.** `File.Delete` is a no-op on a missing file, so check for existence within a single try, and map `FileNotFoundException` to `false`.

### 7.3 DI registration

`AddFileRepositoryInfrastructure(this IServiceCollection, IConfiguration)` registers `IFileRepository` as a singleton (the repository is stateless) and the options.

---

## 8. Web Design

### 8.1 Endpoints (POST-first)

All actions are `POST`, as requested. Actions that take a body use `multipart/form-data` for uploads and `application/json` otherwise.

| Action | Route | Request | Success | Errors |
|---|---|---|---|---|
| Add | `POST /api/files/add` | multipart: `file`, optional `fileName` (defaults to the uploaded file's name) | `201` `FileSummaryResponse` | `400`, `409` |
| Update | `POST /api/files/update` | multipart: `fileName`, `file` | `200` `FileSummaryResponse` | `400`, `404` |
| Delete | `POST /api/files/delete` | JSON: `{ "fileName": "..." }` | `204` | `400`, `404` |
| Get | `POST /api/files/get` | JSON: `{ "fileName": "..." }` | `200` file bytes (`FileStreamResult`) | `400`, `404` |
| List | `POST /api/files/list` | JSON: `{ "namePrefix": null, "skip": 0, "take": 100 }` | `200` `ListFilesResponse` | `400` |

Error bodies use `ProblemDetails`, or `ValidationProblemDetails` for `400`, so Swagger and clients get one consistent shape.

**Trade-off to be aware of:** using `POST` for get, list, and delete is a deliberate departure from REST semantics. Those calls won't be cacheable, can't be linked or opened in a browser address bar, and lose the idempotency hints of `GET` and `DELETE`. It is a reasonable choice for an RPC-style API, and the controller and route names are explicit so the intent is clear.

### 8.2 Controller rules

- `FilesController` is `[ApiController]` and `[Route("api/files")]`, with `[Produces("application/json")]` where applicable.
- The controller has **no business logic**. Each action builds a command or query from the request DTO, calls `IFileService`, and returns `result.ToActionResult(...)`.
- For uploads, `IFormFile.OpenReadStream()` feeds `AddFileCommand.Content`, and `IFormFile.Length` feeds `SizeBytes`.
- For `get`, the content type comes from `FileExtensionContentTypeProvider` with `application/octet-stream` as the fallback. This is a Web concern, so Core stores no content type.
- Every action accepts and forwards a `CancellationToken`.

### 8.3 AppResult to HTTP mapping (`AppResultExtensions`)

| `ResultStatus` | HTTP |
|---|---|
| `Ok` | `200`, or the status the action specifies (`201`, `204`) |
| `Invalid` | `400` `ValidationProblemDetails` built from `ValidationErrors` |
| `NotFound` | `404` `ProblemDetails` |
| `Conflict` | `409` `ProblemDetails` |
| `Error` | `500` `ProblemDetails` |

### 8.4 Cross-cutting

- **Global exception handling** through `IExceptionHandler` plus `UseExceptionHandler`, returning `ProblemDetails` for unexpected exceptions with no internals leaked.
- **Upload limits**: set `FormOptions.MultipartBodyLengthLimit` and Kestrel `MaxRequestBodySize` from `MaxFileSizeBytes`.
- **Swagger**: enabled in Development by default and controllable by config. Turn on XML documentation (`GenerateDocumentationFile`) and add `[ProducesResponseType]` on every action so all status codes appear in the UI. Multipart actions show a file picker.
- `public partial class Program` so `WebApplicationFactory<Program>` can host the app in tests.

---

## 9. Testing Strategy (NUnit)

Naming convention: `Method_Scenario_ExpectedResult`.

### 9.1 `HiveMind.Core.UnitTests` (required by the spec)

`IFileRepository` is mocked with NSubstitute. No disk access.

- **FileNameValidator**: valid names pass. Empty, whitespace, `../x`, `a/b`, `a\b`, `.`, `..`, `.hidden`, over-length, and invalid-character names fail (use `[TestCase]`).
- **AddAsync**: valid input returns `Ok` and calls `TryCreateAsync` once. An existing file returns `Conflict`. An invalid name returns `Invalid` and never calls the repository. Zero-size and oversize input return `Invalid`.
- **UpdateAsync**: `Ok` on success, `NotFound` when the repository returns `null`, and `Invalid` cases.
- **DeleteAsync**: `Ok`, `NotFound`, and `Invalid` name.
- **GetAsync**: `Ok` with content, `NotFound`, and `Invalid` name.
- **ListAsync**: passes prefix, skip, and take through. Negative skip, zero take, and take above 1000 return `Invalid`.
- **AppResult**: the factories set status, messages, and validation errors correctly.

### 9.2 `HiveMind.Infrastructure.Tests`

Runs against a real temp directory (`[SetUp]` creates it, `[TearDown]` deletes it).

- Create then read back returns identical bytes.
- Create twice returns `null` on the second call.
- Replace an existing file changes its content. Replace a missing file returns `null`.
- Delete an existing file returns `true`, and a missing file returns `false`.
- List sorts, filters by prefix, pages, and excludes `.staging`.
- Path traversal attempts (`../evil.txt`) are rejected even when the Core validator is bypassed.
- A failed write leaves no temp file behind and no partial destination file.
- Concurrent creates of the same name: exactly one wins.

### 9.3 `HiveMind.Web.IntegrationTests`

Uses `WebApplicationFactory<Program>` with `RootPath` overridden to a temp directory.

- Happy-path round trip: add, list, get, update, get, delete, list.
- Status codes: `201`, `200`, `204`, `400` (with validation problem body), `404`, `409`.
- The Swagger document is served at `/swagger/v1/swagger.json` and includes all five routes.

---

## 10. Implementation Plan

Work test-first inside Core. Each milestone should leave the build green.

| Milestone | Tasks | Done when |
|---|---|---|
| **0. Scaffold** | Create the solution and the three `src` projects with the reference graph from section 3. Create the three test projects. Add packages. | `dotnet build` and `dotnet test` run clean (no tests yet). |
| **1. Core primitives** | `AppResult`, `PagedResult`, output records, commands and queries, `FileValidationRules`, interfaces. | Core compiles with zero package references. |
| **2. Core logic (TDD)** | `FileNameValidator` and `FileService`, written test-first per section 9.1. | Core unit tests all pass with the repository mocked. |
| **3. Infrastructure** | `FileSystemFileRepository`, options, DI extension, plus the section 9.2 tests. | Infrastructure tests pass against a temp directory. |
| **4. Web** | `Program.cs`, config binding, controller, DTOs, `AppResultExtensions`, exception handler, Swagger. | The app runs, and every action works through Swagger UI. |
| **5. Web integration tests** | Section 9.3 tests. | Full round trip passes in CI. |
| **6. Hardening and docs** | Confirm upload limits, path safety, and startup validation of config. Write a README with run, config, and example `curl` calls. | README steps work from a clean clone. |

### Definition of done

- All five stories meet their acceptance criteria.
- `HiveMind.Core` has no package or project references.
- Every `IFileService` path has a Core unit test.
- Swagger UI documents all endpoints, request shapes, and response codes.
- The repository folder is configurable only through `appsettings` or environment overrides, with no hard-coded paths.

---

## 11. Risks and Open Questions

1. **Sub-folders**: the flat design blocks `folder/file.txt`. If nested paths are needed, the validator, the safe-path check, and list semantics all need to change.
2. **Overwrite semantics**: add-fails-if-exists and update-fails-if-missing are strict. If you want "upsert on add" or an `overwrite` flag, it's a small change in `FileService` and one extra repository method.
3. **Max size and paging defaults**: I proposed 10 MB, a default page size of 100, and a maximum of 1000. Please confirm.
4. **Swagger library**: Swashbuckle versus the built-in OpenAPI package (see section 4).
5. **Authentication, authorization, and quotas** are not covered and would be a separate story.
6. **Deployment**: if the API runs on more than one instance, a local folder is not shared storage. `IFileRepository` makes moving to blob storage a new Infrastructure class with no Core or Web change.

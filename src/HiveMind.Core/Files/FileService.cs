using HiveMind.Core.Common;
namespace HiveMind.Core.Files;
public sealed class FileService(IFileRepository repository, FileValidationRules rules) : IFileService
{
    public async Task<AppResult<FileSummary>> AddAsync(AddFileCommand c, CancellationToken ct)
    {
        var e = Validate(c.FileName, c.SizeBytes); if (e.Count > 0) return AppResult<FileSummary>.Invalid(e);
        var result = await repository.TryCreateAsync(c.FileName, c.Content, ct); return result is null ? AppResult<FileSummary>.Conflict("A file with that name already exists.") : AppResult<FileSummary>.Ok(result);
    }
    public async Task<AppResult<FileSummary>> UpdateAsync(UpdateFileCommand c, CancellationToken ct)
    {
        var e = Validate(c.FileName, c.SizeBytes); if (e.Count > 0) return AppResult<FileSummary>.Invalid(e);
        var result = await repository.TryReplaceAsync(c.FileName, c.Content, ct); return result is null ? AppResult<FileSummary>.NotFound("File was not found.") : AppResult<FileSummary>.Ok(result);
    }
    public async Task<AppResult> DeleteAsync(DeleteFileCommand c, CancellationToken ct)
    {
        var e = FileNameValidator.Validate(c.FileName, rules); if (e.Count > 0) return AppResult.Invalid(e);
        return await repository.TryDeleteAsync(c.FileName, ct) ? AppResult.Ok() : AppResult.NotFound("File was not found.");
    }
    public async Task<AppResult<FileContent>> GetAsync(GetFileQuery q, CancellationToken ct)
    {
        var e = FileNameValidator.Validate(q.FileName, rules); if (e.Count > 0) return AppResult<FileContent>.Invalid(e);
        var result = await repository.TryOpenReadAsync(q.FileName, ct); return result is null ? AppResult<FileContent>.NotFound("File was not found.") : AppResult<FileContent>.Ok(result);
    }
    public async Task<AppResult<PagedResult<FileSummary>>> ListAsync(ListFilesQuery q, CancellationToken ct)
    {
        var e = new List<ValidationError>(); if (q.Skip < 0) e.Add(new("skip", "Skip cannot be negative.")); if (q.Take < 1 || q.Take > 1000) e.Add(new("take", "Take must be between 1 and 1000."));
        if (e.Count > 0) return AppResult<PagedResult<FileSummary>>.Invalid(e);
        return AppResult<PagedResult<FileSummary>>.Ok(await repository.ListAsync(q.NamePrefix, q.Skip, q.Take, ct));
    }
    private IReadOnlyList<ValidationError> Validate(string name, long size) { var e = FileNameValidator.Validate(name, rules).ToList(); if (size <= 0 || size > rules.MaxFileSizeBytes) e.Add(new("file", $"File size must be between 1 and {rules.MaxFileSizeBytes} bytes.")); return e; }
}

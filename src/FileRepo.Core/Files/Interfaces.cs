using FileRepo.Core.Common;
namespace FileRepo.Core.Files;
public interface IFileRepository
{
    Task<FileSummary?> TryCreateAsync(string name, Stream content, CancellationToken ct);
    Task<FileSummary?> TryReplaceAsync(string name, Stream content, CancellationToken ct);
    Task<bool> TryDeleteAsync(string name, CancellationToken ct);
    Task<FileContent?> TryOpenReadAsync(string name, CancellationToken ct);
    Task<PagedResult<FileSummary>> ListAsync(string? namePrefix, int skip, int take, CancellationToken ct);
}
public interface IFileService
{
    Task<AppResult<FileSummary>> AddAsync(AddFileCommand command, CancellationToken ct);
    Task<AppResult<FileSummary>> UpdateAsync(UpdateFileCommand command, CancellationToken ct);
    Task<AppResult> DeleteAsync(DeleteFileCommand command, CancellationToken ct);
    Task<AppResult<FileContent>> GetAsync(GetFileQuery query, CancellationToken ct);
    Task<AppResult<PagedResult<FileSummary>>> ListAsync(ListFilesQuery query, CancellationToken ct);
}

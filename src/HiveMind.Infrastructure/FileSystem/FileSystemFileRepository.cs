using HiveMind.Core.Common;
using HiveMind.Core.Files;
using Microsoft.Extensions.Options;
namespace HiveMind.Infrastructure.FileSystem;
public sealed class FileSystemFileRepository : IFileRepository
{
    private readonly string root, staging;
    public FileSystemFileRepository(IOptions<FileSystemRepositoryOptions> options)
    {
        root = Path.GetFullPath(options.Value.RootPath); Directory.CreateDirectory(root);
        staging = Path.Combine(root, ".staging"); Directory.CreateDirectory(staging);
    }
    public async Task<FileSummary?> TryCreateAsync(string name, Stream content, CancellationToken ct)
    {
        var dest = SafePath(name); var temp = Path.Combine(staging, $"{Guid.NewGuid():N}.tmp");
        try { await WriteTemp(content, temp, ct); try { File.Move(temp, dest, false); } catch (IOException) when (File.Exists(dest)) { return null; } return Summary(new FileInfo(dest)); }
        finally { TryDelete(temp); }
    }
    public async Task<FileSummary?> TryReplaceAsync(string name, Stream content, CancellationToken ct)
    {
        var dest = SafePath(name); var temp = Path.Combine(staging, $"{Guid.NewGuid():N}.tmp");
        try
        {
            await WriteTemp(content, temp, ct);
            if (!File.Exists(dest)) return null;
            try
            {
                File.Move(temp, dest, true);
            }
            catch (FileNotFoundException)
            {
                return null;
            }
            return Summary(new FileInfo(dest));
        }
        finally { TryDelete(temp); }
    }
    public Task<bool> TryDeleteAsync(string name, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested(); var path = SafePath(name);
        try { if (!File.Exists(path)) return Task.FromResult(false); File.Delete(path); return Task.FromResult(true); } catch (FileNotFoundException) { return Task.FromResult(false); }
    }
    public Task<FileContent?> TryOpenReadAsync(string name, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested(); var path = SafePath(name); if (!File.Exists(path)) return Task.FromResult<FileContent?>(null);
        var info = new FileInfo(path); return Task.FromResult<FileContent?>(new FileContent(Summary(info), new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, FileOptions.Asynchronous | FileOptions.SequentialScan)));
    }
    public Task<PagedResult<FileSummary>> ListAsync(string? prefix, int skip, int take, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested(); var all = Directory.EnumerateFiles(root, "*", SearchOption.TopDirectoryOnly).Select(p => Summary(new FileInfo(p))).Where(x => prefix is null || x.Name.StartsWith(prefix, StringComparison.Ordinal)).OrderBy(x => x.Name, StringComparer.Ordinal).ToArray();
        return Task.FromResult(new PagedResult<FileSummary>(all.Skip(skip).Take(take).ToArray(), all.Length));
    }
    private string SafePath(string name) { var full = Path.GetFullPath(Path.Combine(root, name)); if (!full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal) || Path.GetFileName(full) != name) throw new ArgumentException("Invalid file path.", nameof(name)); return full; }
    private static async Task WriteTemp(Stream input, string path, CancellationToken ct) { await using var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, FileOptions.Asynchronous | FileOptions.SequentialScan); await input.CopyToAsync(output, ct); await output.FlushAsync(ct); }
    private static FileSummary Summary(FileInfo i) => new(i.Name, i.Length, i.CreationTimeUtc, i.LastWriteTimeUtc);
    private static void TryDelete(string p) { try { if (File.Exists(p)) File.Delete(p); } catch { } }
}

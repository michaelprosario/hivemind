using FileRepo.Infrastructure.FileSystem;
using Microsoft.Extensions.Options;
namespace FileRepo.Infrastructure.Tests;
public class FileSystemRepositoryTests
{
    private string path = null!; private FileSystemFileRepository repo = null!;
    [SetUp] public void SetUp() { path = Path.Combine(Path.GetTempPath(), "filerepo-" + Guid.NewGuid()); repo = new(Options.Create(new FileSystemRepositoryOptions { RootPath = path })); }
    [TearDown] public void TearDown() { if (Directory.Exists(path)) Directory.Delete(path, true); }
    [Test] public async Task CreateAndRead_RoundTripsBytes() { await repo.TryCreateAsync("a.txt", new MemoryStream([1, 2, 3]), default); var content = await repo.TryOpenReadAsync("a.txt", default); using var ms = new MemoryStream(); await content!.Content.CopyToAsync(ms); Assert.That(ms.ToArray(), Is.EqualTo(new byte[] { 1, 2, 3 })); }
    [Test] public async Task DuplicateCreate_ReturnsNull() { await repo.TryCreateAsync("a.txt", new MemoryStream([1]), default); Assert.That(await repo.TryCreateAsync("a.txt", new MemoryStream([2]), default), Is.Null); }
    [Test] public async Task ReplaceMissing_ReturnsNull() => Assert.That(await repo.TryReplaceAsync("missing.txt", new MemoryStream([1]), default), Is.Null);
    [Test] public async Task Traversal_IsRejected() => Assert.ThrowsAsync<ArgumentException>(async () => await repo.TryOpenReadAsync("../evil.txt", default));
}

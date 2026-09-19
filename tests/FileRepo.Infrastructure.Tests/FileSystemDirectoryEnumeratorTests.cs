using FileRepo.Infrastructure.VectorSearch;
using Microsoft.Extensions.Options;

namespace FileRepo.Infrastructure.Tests;

public sealed class FileSystemDirectoryEnumeratorTests
{
    private string root = null!;

    [SetUp]
    public void SetUp() => root = Path.Combine(Path.GetTempPath(), "filerepo-ingest-" + Guid.NewGuid());

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(root))
            Directory.Delete(root, true);
    }

    [Test]
    public async Task Enumerate_UsesRecursionFlagAndOrdinalOrdering()
    {
        Directory.CreateDirectory(Path.Combine(root, "nested"));
        await File.WriteAllTextAsync(Path.Combine(root, "z.md"), "z");
        await File.WriteAllTextAsync(Path.Combine(root, "a.txt"), "a");
        await File.WriteAllTextAsync(Path.Combine(root, "nested", "b.md"), "b");
        await File.WriteAllTextAsync(Path.Combine(root, "image.png"), "image");
        var enumerator = CreateEnumerator();

        var topLevel = await enumerator.EnumerateAsync(root, false, default);
        var recursive = await enumerator.EnumerateAsync(root, true, default);

        Assert.That(topLevel.Value, Is.EqualTo([
            Path.Combine(root, "a.txt"), Path.Combine(root, "image.png"), Path.Combine(root, "z.md")]));
        Assert.That(recursive.Value, Is.EqualTo([
            Path.Combine(root, "a.txt"), Path.Combine(root, "image.png"),
            Path.Combine(root, "nested", "b.md"), Path.Combine(root, "z.md")]));
    }

    [Test]
    public async Task Enumerate_RejectsEscapeAndBatchLimit()
    {
        Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(Path.Combine(root, "a.md"), "a");
        await File.WriteAllTextAsync(Path.Combine(root, "b.md"), "b");
        var enumerator = new FileSystemDirectoryEnumerator(Options.Create(
            new VectorSearchOptions { AllowedSourceRoot = root, MaxFilesPerDirectoryIngest = 1 }));

        var overLimit = await enumerator.EnumerateAsync(root, false, default);
        var escape = await enumerator.EnumerateAsync(Path.Combine(root, "..", "outside"), false, default);

        Assert.That(overLimit.Status, Is.EqualTo(FileRepo.Core.Common.ResultStatus.Invalid));
        Assert.That(overLimit.ValidationErrors[0].Message, Does.Contain("1"));
        Assert.That(escape.Status, Is.EqualTo(FileRepo.Core.Common.ResultStatus.Invalid));
    }

    private FileSystemDirectoryEnumerator CreateEnumerator() => new(Options.Create(
        new VectorSearchOptions { AllowedSourceRoot = root, MaxFilesPerDirectoryIngest = 10 }));
}

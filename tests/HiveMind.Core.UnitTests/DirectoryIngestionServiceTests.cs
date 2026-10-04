using HiveMind.Core.Common;
using HiveMind.Core.VectorSearch;
using NSubstitute;

namespace HiveMind.Core.UnitTests;

public sealed class DirectoryIngestionServiceTests
{
    [Test]
    public async Task IngestDirectory_ReportsOutcomesAndContinuesAfterFailure()
    {
        var enumerator = Substitute.For<IDirectoryFileEnumerator>();
        var documents = Substitute.For<IVectorDocumentService>();
        var first = "/root/a.md";
        var second = "/root/b.png";
        var third = "/root/c.md";
        enumerator.EnumerateAsync("/root", false, Arg.Any<CancellationToken>())
            .Returns(AppResult<IReadOnlyList<string>>.Ok([first, second, third]));
        documents.IngestAsync(new("docs_v1", first), Arg.Any<CancellationToken>())
            .Returns(AppResult<IngestedVectorDocument>.Ok(new(
                "docs_v1", "a.md", first, "sha256:a", "v1", 1)));
        documents.IngestAsync(new("docs_v1", second), Arg.Any<CancellationToken>())
            .Returns(AppResult<IngestedVectorDocument>.Invalid(
                [new("sourcePath", "Source file extension is not supported.")]));
        documents.IngestAsync(new("docs_v1", third), Arg.Any<CancellationToken>())
            .Returns(AppResult<IngestedVectorDocument>.NotFound("Source file was not found."));

        var result = await new DirectoryIngestionService(enumerator, documents)
            .IngestDirectoryAsync(new("docs_v1", "/root"), default);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value!.FilesDiscovered, Is.EqualTo(3));
        Assert.That(result.Value.IngestedCount, Is.EqualTo(1));
        Assert.That(result.Value.SkippedCount, Is.EqualTo(1));
        Assert.That(result.Value.FailedCount, Is.EqualTo(1));
        Assert.That(result.Value.Files[1].Reason, Is.EqualTo("Extension not supported."));
        await documents.Received(1).IngestAsync(new("docs_v1", third), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task IngestDirectory_InvalidIndexDoesNotEnumerate()
    {
        var enumerator = Substitute.For<IDirectoryFileEnumerator>();
        var documents = Substitute.For<IVectorDocumentService>();

        var result = await new DirectoryIngestionService(enumerator, documents)
            .IngestDirectoryAsync(new("Invalid", "/root"), default);

        Assert.That(result.Status, Is.EqualTo(ResultStatus.Invalid));
        await enumerator.DidNotReceive().EnumerateAsync(
            Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }
}

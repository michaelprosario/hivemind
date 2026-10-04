using HiveMind.Core.Common;
using HiveMind.Core.Files;
using NSubstitute;
namespace HiveMind.Core.UnitTests;
public class FileServiceTests
{
    private IFileRepository repo = null!;
    private FileService service = null!;
    [SetUp] public void SetUp() { repo = Substitute.For<IFileRepository>(); service = new(repo, new() { MaxFileSizeBytes = 10 }); }
    [Test] public async Task Add_Valid_ReturnsOk() { var summary = new FileSummary("a.txt", 1, DateTime.UtcNow, DateTime.UtcNow); repo.TryCreateAsync("a.txt", Arg.Any<Stream>(), Arg.Any<CancellationToken>()).Returns(summary); var result = await service.AddAsync(new("a.txt", new MemoryStream([1]), 1), default); Assert.That(result.Status, Is.EqualTo(ResultStatus.Ok)); }
    [Test] public async Task Add_Existing_ReturnsConflict() { repo.TryCreateAsync(Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<CancellationToken>()).Returns((FileSummary?)null); var result = await service.AddAsync(new("a.txt", new MemoryStream([1]), 1), default); Assert.That(result.Status, Is.EqualTo(ResultStatus.Conflict)); }
    [TestCase("")][TestCase("../evil.txt")][TestCase(".hidden")] public async Task Add_InvalidName_DoesNotCallRepository(string name) { var result = await service.AddAsync(new(name, new MemoryStream([1]), 1), default); Assert.That(result.Status, Is.EqualTo(ResultStatus.Invalid)); await repo.DidNotReceive().TryCreateAsync(Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<CancellationToken>()); }
    [Test] public async Task List_InvalidPaging_ReturnsInvalid() { var result = await service.ListAsync(new(null, -1, 100), default); Assert.That(result.Status, Is.EqualTo(ResultStatus.Invalid)); }
}

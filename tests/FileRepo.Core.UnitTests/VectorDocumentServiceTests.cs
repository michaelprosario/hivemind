using FileRepo.Core.Common;
using FileRepo.Core.VectorSearch;
using NSubstitute;

namespace FileRepo.Core.UnitTests;

public sealed class VectorDocumentServiceTests
{
    private ISourceTextReader sourceReader = null!;
    private ITextEmbeddingProvider embeddingProvider = null!;
    private IVectorDocumentStore store = null!;
    private VectorDocumentService service = null!;

    [SetUp]
    public void SetUp()
    {
        sourceReader = Substitute.For<ISourceTextReader>();
        embeddingProvider = Substitute.For<ITextEmbeddingProvider>();
        store = Substitute.For<IVectorDocumentStore>();
        embeddingProvider.VectorSize.Returns(3);
        embeddingProvider.ModelRevision.Returns("test-model");
        service = new VectorDocumentService(
            sourceReader,
            new TextChunker(new VectorSearchValidationRules { ChunkSizeCharacters = 10, ChunkOverlapCharacters = 2 }),
            embeddingProvider,
            store,
            new VectorSearchValidationRules { MaxQueryLength = 20 });
    }

    [TestCase("")]
    [TestCase("Uppercase")]
    [TestCase("has spaces")]
    [TestCase("-starts-with-dash")]
    public async Task Search_InvalidIndex_ReturnsInvalidWithoutEmbedding(string index)
    {
        var result = await service.SearchAsync(new(index, "valid query"), default);

        Assert.That(result.Status, Is.EqualTo(ResultStatus.Invalid));
        await embeddingProvider.DidNotReceive().EmbedQueryAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Search_ValidRequest_EmbedsAndSearchesSpecifiedIndex()
    {
        embeddingProvider.EmbedQueryAsync("architecture", Arg.Any<CancellationToken>())
            .Returns(new ReadOnlyMemory<float>([1, 0, 0]));
        store.SearchAsync("docs_v1", Arg.Any<ReadOnlyMemory<float>>(), 2, Arg.Any<CancellationToken>())
            .Returns(AppResult<IReadOnlyList<VectorSearchHit>>.Ok([]));

        var result = await service.SearchAsync(new("docs_v1", " architecture ", 2), default);

        Assert.That(result.IsSuccess, Is.True);
        await store.Received(1).SearchAsync("docs_v1", Arg.Any<ReadOnlyMemory<float>>(), 2, Arg.Any<CancellationToken>());
    }

    [Test]
    public void TextChunker_PreservesOffsetsAndOverlap()
    {
        var chunks = new TextChunker(new VectorSearchValidationRules
        {
            ChunkSizeCharacters = 10,
            ChunkOverlapCharacters = 2
        }).Chunk("one two three four");

        Assert.That(chunks, Has.Count.GreaterThan(1));
        Assert.That(chunks[0].StartCharacter, Is.EqualTo(0));
        Assert.That(chunks[1].StartCharacter, Is.LessThan(chunks[0].EndCharacter));
        Assert.That(chunks.All(x => x.Text.Length > 0), Is.True);
    }
}

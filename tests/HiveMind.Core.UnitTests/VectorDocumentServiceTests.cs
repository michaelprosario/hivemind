using HiveMind.Core.Common;
using HiveMind.Core.VectorSearch;
using NSubstitute;

namespace HiveMind.Core.UnitTests;

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
    public async Task InspectIndexSchema_ValidRequest_InspectsSpecifiedCollection()
    {
        store.InspectIndexSchemaAsync("incident_triage", Arg.Any<CancellationToken>())
            .Returns(AppResult<VectorIndexSchema>.Ok(new(
                "incident_triage", [], [], 0, 0, "green")));

        var result = await service.InspectIndexSchemaAsync(new("incident_triage"), default);

        Assert.That(result.IsSuccess, Is.True);
        await store.Received(1).InspectIndexSchemaAsync("incident_triage", Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task InspectIndexSchema_InvalidCollectionName_ReturnsInvalidWithoutStoreCall()
    {
        var result = await service.InspectIndexSchemaAsync(new("Incident Triage"), default);

        Assert.That(result.Status, Is.EqualTo(ResultStatus.Invalid));
        Assert.That(result.ValidationErrors.Any(e => e.Field == "collectionName"), Is.True);
        await store.DidNotReceive().InspectIndexSchemaAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task CreateIndex_ValidRequest_UsesEmbeddingDimensions()
    {
        store.CreateIndexAsync("incident_triage", 3, Arg.Any<CancellationToken>())
            .Returns(AppResult<VectorIndexInfo>.Ok(new(
                "incident_triage", 3, "Cosine", 0, "created", true)));

        var result = await service.CreateIndexAsync(new("incident_triage"), default);

        Assert.That(result.IsSuccess, Is.True);
        await store.Received(1).CreateIndexAsync("incident_triage", 3, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task CreateIndex_InvalidName_ReturnsInvalidWithoutStoreCall()
    {
        var result = await service.CreateIndexAsync(new("Incident Triage"), default);

        Assert.That(result.Status, Is.EqualTo(ResultStatus.Invalid));
        Assert.That(result.ValidationErrors.Any(e => e.Field == "name"), Is.True);
        await store.DidNotReceive().CreateIndexAsync(
            Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
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

    [TestCase("")]
    [TestCase("Uppercase")]
    [TestCase("has spaces")]
    [TestCase("-starts-with-dash")]
    public async Task StoreMemory_InvalidIndex_ReturnsInvalidWithoutEmbedding(string index)
    {
        var result = await service.StoreMemoryAsync(new(index, "some memory text", null, null, null, null), default);

        Assert.That(result.Status, Is.EqualTo(ResultStatus.Invalid));
        await embeddingProvider.DidNotReceive().EmbedDocumentsAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task StoreMemory_EmptyText_ReturnsInvalid()
    {
        var result = await service.StoreMemoryAsync(new("agent_memory", "   ", null, null, null, null), default);

        Assert.That(result.Status, Is.EqualTo(ResultStatus.Invalid));
        Assert.That(result.ValidationErrors.Any(e => e.Field == "text"), Is.True);
    }

    [Test]
    public async Task StoreMemory_TextOverLimit_ReturnsInvalid()
    {
        service = new VectorDocumentService(
            sourceReader,
            new TextChunker(new VectorSearchValidationRules { ChunkSizeCharacters = 10, ChunkOverlapCharacters = 2 }),
            embeddingProvider,
            store,
            new VectorSearchValidationRules { MaxQueryLength = 20, MaxMemoryTextLength = 5 });

        var result = await service.StoreMemoryAsync(new("agent_memory", "this is too long", null, null, null, null), default);

        Assert.That(result.Status, Is.EqualTo(ResultStatus.Invalid));
        Assert.That(result.ValidationErrors.Any(e => e.Field == "text"), Is.True);
    }

    [TestCase("has spaces")]
    [TestCase("")]
    [TestCase("has/slash")]
    public async Task StoreMemory_InvalidKey_ReturnsInvalid(string key)
    {
        var result = await service.StoreMemoryAsync(new("agent_memory", "valid text", key, null, null, null), default);

        Assert.That(result.Status, Is.EqualTo(ResultStatus.Invalid));
        Assert.That(result.ValidationErrors.Any(e => e.Field == "key"), Is.True);
    }

    [Test]
    public async Task StoreMemory_TooManyTags_ReturnsInvalid()
    {
        var tags = Enumerable.Range(0, 40).Select(i => $"tag{i}").ToArray();

        var result = await service.StoreMemoryAsync(new("agent_memory", "valid text", null, tags, null, null), default);

        Assert.That(result.Status, Is.EqualTo(ResultStatus.Invalid));
        Assert.That(result.ValidationErrors.Any(e => e.Field == "tags"), Is.True);
    }

    [Test]
    public async Task StoreMemory_NonAbsoluteSourceUrl_ReturnsInvalid()
    {
        var result = await service.StoreMemoryAsync(new("agent_memory", "valid text", null, null, null, "not-a-url"), default);

        Assert.That(result.Status, Is.EqualTo(ResultStatus.Invalid));
        Assert.That(result.ValidationErrors.Any(e => e.Field == "sourceUrl"), Is.True);
    }

    [Test]
    public async Task StoreMemory_ValidRequestWithoutKey_GeneratesKeyAndStores()
    {
        embeddingProvider.EmbedDocumentsAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(ci => ((IReadOnlyList<string>)ci[0]).Select(_ => new ReadOnlyMemory<float>([1, 0, 0])).ToArray());
        store.StoreMemoryAsync("agent_memory", Arg.Any<MemoryRecord>(), Arg.Any<IReadOnlyList<VectorChunk>>(),
                Arg.Any<IReadOnlyList<ReadOnlyMemory<float>>>(), 3, "test-model", Arg.Any<CancellationToken>())
            .Returns(ci => AppResult<StoredMemory>.Ok(new("agent_memory", ((MemoryRecord)ci[1]).Key, ((MemoryRecord)ci[1]).ContentHash, 1,
                DateTimeOffset.UtcNow, DateTimeOffset.UtcNow)));

        var result = await service.StoreMemoryAsync(new("agent_memory", "remember this", null, ["a", "a", " b "], "alice", "https://example.com"), default);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value!.Key, Is.Not.Empty);
        await store.Received(1).StoreMemoryAsync("agent_memory",
            Arg.Is<MemoryRecord>(m => !m.IsCallerSuppliedKey && m.Tags.SequenceEqual(new[] { "a", "b" }) && m.Author == "alice" && m.SourceUrl == "https://example.com"),
            Arg.Any<IReadOnlyList<VectorChunk>>(), Arg.Any<IReadOnlyList<ReadOnlyMemory<float>>>(), 3, "test-model", Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task StoreMemory_ValidRequestWithKey_PassesCallerSuppliedKey()
    {
        embeddingProvider.EmbedDocumentsAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(ci => ((IReadOnlyList<string>)ci[0]).Select(_ => new ReadOnlyMemory<float>([1, 0, 0])).ToArray());
        store.StoreMemoryAsync("agent_memory", Arg.Any<MemoryRecord>(), Arg.Any<IReadOnlyList<VectorChunk>>(),
                Arg.Any<IReadOnlyList<ReadOnlyMemory<float>>>(), 3, "test-model", Arg.Any<CancellationToken>())
            .Returns(ci => AppResult<StoredMemory>.Ok(new("agent_memory", ((MemoryRecord)ci[1]).Key, ((MemoryRecord)ci[1]).ContentHash, 1,
                DateTimeOffset.UtcNow, DateTimeOffset.UtcNow)));

        var result = await service.StoreMemoryAsync(new("agent_memory", "remember this", "task-progress-1", null, null, null), default);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value!.Key, Is.EqualTo("task-progress-1"));
        await store.Received(1).StoreMemoryAsync("agent_memory",
            Arg.Is<MemoryRecord>(m => m.IsCallerSuppliedKey && m.Key == "task-progress-1"),
            Arg.Any<IReadOnlyList<VectorChunk>>(), Arg.Any<IReadOnlyList<ReadOnlyMemory<float>>>(), 3, "test-model", Arg.Any<CancellationToken>());
    }
}

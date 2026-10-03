using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FileRepo.Core.VectorSearch;
using FileRepo.Infrastructure.VectorSearch;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace FileRepo.Infrastructure.Tests;

public class QdrantVectorDocumentStoreTests
{
    private const string IndexName = "agent_memory";

    private static QdrantVectorDocumentStore CreateStore(FakeQdrantHandler handler)
    {
        var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:6333") };
        var options = Options.Create(new VectorSearchOptions
        {
            AllowedSourceRoot = Path.GetTempPath(),
            ModelDirectory = Path.GetTempPath(),
            ModelRevision = "test-model",
            CatalogPath = Path.Combine(Path.GetTempPath(), "filerepo-" + Guid.NewGuid() + ".json"),
            QdrantEndpoint = "http://localhost:6333"
        });
        return new QdrantVectorDocumentStore(client, options, NullLogger<QdrantVectorDocumentStore>.Instance);
    }

    private static IReadOnlyList<VectorChunk> Chunks(params string[] texts) =>
        texts.Select((t, i) => new VectorChunk(i, 0, t.Length, t)).ToArray();

    private static IReadOnlyList<ReadOnlyMemory<float>> Vectors(int count, int size = 3) =>
        Enumerable.Range(0, count).Select(_ => new ReadOnlyMemory<float>(new float[size])).ToArray();

    [Test]
    public async Task StoreMemoryAsync_NewKey_CreatesCollectionAndWritesPointsWithMetadata()
    {
        var handler = new FakeQdrantHandler();
        handler.OnGetCollection = IndexName => new HttpResponseMessage(HttpStatusCode.NotFound);
        handler.OnScroll = () => JsonSerializer.Serialize(new { result = new { points = Array.Empty<object>() } });
        var store = CreateStore(handler);

        var memory = new MemoryRecord("task-1", true, "sha256:abc", ["work", "progress"], "alice", "https://example.com");
        var result = await store.StoreMemoryAsync(IndexName, memory, Chunks("remember this"), Vectors(1), 3, "test-model", default);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value!.Key, Is.EqualTo("task-1"));
        Assert.That(result.Value!.ChunkCount, Is.EqualTo(1));

        var createCollection = handler.Requests.Single(r => r.Method == HttpMethod.Put && r.Path == $"/collections/{IndexName}");
        Assert.That(createCollection.Body, Does.Contain("\"size\":3"));

        var upsert = handler.Requests.Single(r => r.Method == HttpMethod.Put && r.Path == $"/collections/{IndexName}/points?wait=true");
        Assert.That(upsert.Body, Does.Contain("\"kind\":\"memory\""));
        Assert.That(upsert.Body, Does.Contain("\"key\":\"task-1\""));
        Assert.That(upsert.Body, Does.Contain("\"author\":\"alice\""));
        Assert.That(upsert.Body, Does.Contain("\"sourceUrl\":\"https://example.com\""));
        Assert.That(upsert.Body, Does.Contain("\"work\""));

        var delete = handler.Requests.Single(r => r.Method == HttpMethod.Post && r.Path == $"/collections/{IndexName}/points/delete?wait=true");
        Assert.That(delete.Body, Does.Contain("\"gte\":1"));
    }

    [Test]
    public async Task StoreMemoryAsync_ExistingKey_PreservesOriginalCreatedAt()
    {
        var originalCreatedAt = DateTimeOffset.Parse("2024-01-01T00:00:00Z");
        var handler = new FakeQdrantHandler();
        handler.OnGetCollection = _ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new
            {
                result = new { config = new { @params = new { vectors = new { size = 3, distance = "Cosine" } } } }
            })
        };
        handler.OnScroll = () => JsonSerializer.Serialize(new
        {
            result = new
            {
                points = new[] { new { payload = new { createdAt = originalCreatedAt.ToString("O") } } }
            }
        });
        var store = CreateStore(handler);

        var memory = new MemoryRecord("task-1", true, "sha256:abc", [], null, null);
        var result = await store.StoreMemoryAsync(IndexName, memory, Chunks("updated text"), Vectors(1), 3, "test-model", default);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value!.CreatedAt, Is.EqualTo(originalCreatedAt));
        Assert.That(result.Value!.UpdatedAt, Is.GreaterThan(originalCreatedAt));
        Assert.That(handler.Requests.Any(r => r.Method == HttpMethod.Put && r.Path == $"/collections/{IndexName}"), Is.False);
    }

    [Test]
    public async Task InspectIndexSchemaAsync_ReturnsPayloadFieldsAndCounts()
    {
        var handler = new FakeQdrantHandler
        {
            OnGetCollection = _ => JsonResponse(HttpStatusCode.OK, new
            {
                result = new
                {
                    status = "green",
                    points_count = 12,
                    indexed_vectors_count = 12,
                    payload_schema = new
                    {
                        status = new { data_type = "keyword", points = 8 },
                        is_open = new { data_type = "bool", points = 5 }
                    }
                }
            })
        };
        var store = CreateStore(handler);

        var result = await store.InspectIndexSchemaAsync(IndexName, default);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value!.PointsCount, Is.EqualTo(12));
        Assert.That(result.Value.VectorCount, Is.EqualTo(12));
        Assert.That(result.Value.IndexedFields, Is.EquivalentTo(new[] { "status", "is_open" }));
        Assert.That(result.Value.PayloadSchema.Single(f => f.Name == "status").DataType, Is.EqualTo("keyword"));
        Assert.That(result.Value.PayloadSchema.Single(f => f.Name == "is_open").DataType, Is.EqualTo("bool"));
    }

    [Test]
    public async Task InspectIndexSchemaAsync_MissingCollection_ReturnsNotFound()
    {
        var handler = new FakeQdrantHandler
        {
            OnGetCollection = _ => new HttpResponseMessage(HttpStatusCode.NotFound)
        };

        var result = await CreateStore(handler).InspectIndexSchemaAsync(IndexName, default);

        Assert.That(result.Status, Is.EqualTo(FileRepo.Core.Common.ResultStatus.NotFound));
    }

    [Test]
    public async Task CreateIndexAsync_CreatesCosineCollectionAtEmbeddingSize()
    {
        var handler = new FakeQdrantHandler
        {
            OnGetCollection = _ => new HttpResponseMessage(HttpStatusCode.NotFound)
        };

        var result = await CreateStore(handler).CreateIndexAsync(IndexName, 384, default);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value!.VectorSize, Is.EqualTo(384));
        Assert.That(result.Value.Distance, Is.EqualTo("Cosine"));
        var creation = handler.Requests.Single(r =>
            r.Method == HttpMethod.Put && r.Path == $"/collections/{IndexName}");
        Assert.That(creation.Body, Does.Contain("\"size\":384"));
        Assert.That(creation.Body, Does.Contain("\"distance\":\"Cosine\""));
    }

    [Test]
    public async Task CreateIndexAsync_ExistingCollection_ReturnsConflict()
    {
        var handler = new FakeQdrantHandler
        {
            OnGetCollection = _ => JsonResponse(HttpStatusCode.OK, new { result = new { } })
        };

        var result = await CreateStore(handler).CreateIndexAsync(IndexName, 384, default);

        Assert.That(result.Status, Is.EqualTo(FileRepo.Core.Common.ResultStatus.Conflict));
        Assert.That(handler.Requests.Any(r => r.Method == HttpMethod.Put), Is.False);
    }

    [Test]
    public async Task CreateIndexAsync_ConcurrentCreationConflict_ReturnsConflict()
    {
        var handler = new FakeQdrantHandler
        {
            OnGetCollection = _ => new HttpResponseMessage(HttpStatusCode.NotFound),
            OnCreateCollection = _ => new HttpResponseMessage(HttpStatusCode.Conflict)
        };

        var result = await CreateStore(handler).CreateIndexAsync(IndexName, 384, default);

        Assert.That(result.Status, Is.EqualTo(FileRepo.Core.Common.ResultStatus.Conflict));
    }

    [Test]
    public async Task InspectIndexSchemaAsync_QdrantFailure_ReturnsError()
    {
        var handler = new FakeQdrantHandler
        {
            OnGetCollection = _ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            {
                Content = new StringContent("Qdrant unavailable")
            }
        };

        var result = await CreateStore(handler).InspectIndexSchemaAsync(IndexName, default);

        Assert.That(result.Status, Is.EqualTo(FileRepo.Core.Common.ResultStatus.Error));
        Assert.That(result.Messages.Single(), Does.Contain("503"));
    }

    private static HttpResponseMessage JsonResponse(HttpStatusCode statusCode, object body) =>
        new(statusCode) { Content = JsonContent.Create(body) };

    private sealed class FakeQdrantHandler : HttpMessageHandler
    {
        public List<RecordedRequest> Requests { get; } = [];
        public Func<string, HttpResponseMessage>? OnGetCollection { get; set; }
        public Func<string, HttpResponseMessage>? OnCreateCollection { get; set; }
        public Func<string>? OnScroll { get; set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(ct);
            var path = request.RequestUri!.PathAndQuery;
            Requests.Add(new RecordedRequest(request.Method, path, body));

            if (request.Method == HttpMethod.Get && path.StartsWith("/collections/", StringComparison.Ordinal))
                return OnGetCollection?.Invoke(path) ?? new HttpResponseMessage(HttpStatusCode.NotFound);

            if (request.Method == HttpMethod.Put &&
                path.StartsWith("/collections/", StringComparison.Ordinal) &&
                !path["/collections/".Length..].Contains("/", StringComparison.Ordinal) &&
                OnCreateCollection is not null)
                return OnCreateCollection(path);

            if (request.Method == HttpMethod.Post && path.EndsWith("/points/scroll", StringComparison.Ordinal))
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(OnScroll?.Invoke() ?? JsonSerializer.Serialize(new { result = new { points = Array.Empty<object>() } }))
                    { Headers = { ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json") } }
                };

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}")
                { Headers = { ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json") } }
            };
        }
    }

    private sealed record RecordedRequest(HttpMethod Method, string Path, string Body);
}

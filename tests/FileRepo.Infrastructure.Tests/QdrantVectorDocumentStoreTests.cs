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

    private sealed class FakeQdrantHandler : HttpMessageHandler
    {
        public List<RecordedRequest> Requests { get; } = [];
        public Func<string, HttpResponseMessage>? OnGetCollection { get; set; }
        public Func<string>? OnScroll { get; set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(ct);
            var path = request.RequestUri!.PathAndQuery;
            Requests.Add(new RecordedRequest(request.Method, path, body));

            if (request.Method == HttpMethod.Get && path.StartsWith("/collections/", StringComparison.Ordinal))
                return OnGetCollection?.Invoke(path) ?? new HttpResponseMessage(HttpStatusCode.NotFound);

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

using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FileRepo.Core.Common;
using FileRepo.Core.VectorSearch;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FileRepo.Infrastructure.VectorSearch;

public sealed class QdrantVectorDocumentStore(
    HttpClient client,
    IOptions<VectorSearchOptions> options,
    ILogger<QdrantVectorDocumentStore> logger) : IVectorDocumentStore
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> SourceLocks = new(StringComparer.Ordinal);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<AppResult<IngestedVectorDocument>> IngestAsync(
        string vectorIndexName,
        SourceText source,
        IReadOnlyList<VectorChunk> chunks,
        IReadOnlyList<ReadOnlyMemory<float>> vectors,
        int vectorSize,
        string modelRevision,
        CancellationToken ct)
    {
        var gate = SourceLocks.GetOrAdd($"{vectorIndexName}\0{source.CanonicalSourcePath}", _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct);
        try
        {
            var catalog = await ReadCatalogAsync(ct);
            var key = CatalogKey(vectorIndexName, source.CanonicalSourcePath);
            if (catalog.TryGetValue(key, out var existing) && existing.ContentHash == source.ContentHash)
                return AppResult<IngestedVectorDocument>.Ok(existing.ToResult(vectorIndexName, source));

            await EnsureCollectionAsync(vectorIndexName, vectorSize, ct);
            var version = Guid.NewGuid().ToString("N");
            var pointIds = chunks.Select(chunk => DeterministicGuid($"{vectorIndexName}\n{source.CanonicalSourcePath}\n{version}\n{chunk.ChunkIndex}").ToString()).ToArray();
            var points = chunks.Select((chunk, index) => new
            {
                id = pointIds[index],
                vector = vectors[index].ToArray(),
                payload = new
                {
                    sourcePath = source.CanonicalSourcePath,
                    sourceName = source.SourceName,
                    contentHash = source.ContentHash,
                    documentVersion = version,
                    isActive = false,
                    chunkIndex = chunk.ChunkIndex,
                    startCharacter = chunk.StartCharacter,
                    endCharacter = chunk.EndCharacter,
                    text = chunk.Text,
                    modelRevision
                }
            }).ToArray();
            await SendSuccessAsync(HttpMethod.Put, $"/collections/{vectorIndexName}/points?wait=true", new { points }, ct);

            catalog[key] = new CatalogEntry(source.ContentHash, version, chunks.Count);
            await WriteCatalogAsync(catalog, ct);
            await SendSuccessAsync(HttpMethod.Post, $"/collections/{vectorIndexName}/points/payload?wait=true",
                new { payload = new { isActive = false }, filter = SourceFilter(source.CanonicalSourcePath) }, ct);
            await SendSuccessAsync(HttpMethod.Post, $"/collections/{vectorIndexName}/points/payload?wait=true",
                new { payload = new { isActive = true }, points = pointIds }, ct);
            await SendSuccessAsync(HttpMethod.Post, $"/collections/{vectorIndexName}/points/delete?wait=true",
                new { filter = OldVersionFilter(source.CanonicalSourcePath, version) }, ct);

            return AppResult<IngestedVectorDocument>.Ok(new(
                vectorIndexName, source.SourceName, source.CanonicalSourcePath,
                source.ContentHash, version, chunks.Count));
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<AppResult<IReadOnlyList<VectorSearchHit>>> SearchAsync(
        string vectorIndexName,
        ReadOnlyMemory<float> queryVector,
        int take,
        CancellationToken ct)
    {
        using var response = await client.GetAsync($"/collections/{vectorIndexName}", ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return AppResult<IReadOnlyList<VectorSearchHit>>.NotFound("Vector index was not found.");
        await EnsureSuccessAsync(response, ct);

        var request = new
        {
            vector = queryVector.ToArray(),
            limit = take,
            with_payload = true,
            filter = new { must = new[] { new { key = "isActive", match = new { value = true } } } }
        };
        using var searchResponse = await client.PostAsJsonAsync($"/collections/{vectorIndexName}/points/search", request, JsonOptions, ct);
        await EnsureSuccessAsync(searchResponse, ct);
        using var document = await JsonDocument.ParseAsync(await searchResponse.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        var results = new List<VectorSearchHit>();
        foreach (var point in document.RootElement.GetProperty("result").EnumerateArray())
        {
            var payload = point.GetProperty("payload");
            results.Add(new(
                vectorIndexName,
                payload.GetProperty("sourceName").GetString() ?? "",
                payload.GetProperty("sourcePath").GetString() ?? "",
                payload.GetProperty("chunkIndex").GetInt32(),
                payload.GetProperty("startCharacter").GetInt32(),
                payload.GetProperty("endCharacter").GetInt32(),
                payload.GetProperty("text").GetString() ?? "",
                point.GetProperty("score").GetSingle()));
        }
        return AppResult<IReadOnlyList<VectorSearchHit>>.Ok(results);
    }

    private async Task EnsureCollectionAsync(string vectorIndexName, int vectorSize, CancellationToken ct)
    {
        using var response = await client.GetAsync($"/collections/{vectorIndexName}", ct);
        if (response.IsSuccessStatusCode)
        {
            using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
            var vectors = document.RootElement.GetProperty("result").GetProperty("config").GetProperty("params").GetProperty("vectors");
            if (vectors.GetProperty("size").GetInt32() != vectorSize ||
                !string.Equals(vectors.GetProperty("distance").GetString(), "Cosine", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Vector index '{vectorIndexName}' has an incompatible vector schema.");
            return;
        }
        if (response.StatusCode != HttpStatusCode.NotFound)
        {
            await EnsureSuccessAsync(response, ct);
            return;
        }
        await SendSuccessAsync(HttpMethod.Put, $"/collections/{vectorIndexName}", new
        {
            vectors = new { size = vectorSize, distance = "Cosine" }
        }, ct);
        await SendSuccessAsync(HttpMethod.Put, $"/collections/{vectorIndexName}/index", new
        {
            field_name = "sourcePath",
            field_schema = "keyword"
        }, ct);
    }

    private async Task SendSuccessAsync(HttpMethod method, string path, object body, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, path) { Content = JsonContent.Create(body, options: JsonOptions) };
        using var response = await client.SendAsync(request, ct);
        await EnsureSuccessAsync(response, ct);
    }

    private async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode) return;
        var detail = await response.Content.ReadAsStringAsync(ct);
        logger.LogError("Qdrant request {Method} {Uri} failed with {StatusCode}: {Detail}",
            response.RequestMessage?.Method, response.RequestMessage?.RequestUri, (int)response.StatusCode, detail);
        throw new HttpRequestException($"Qdrant request failed with status {(int)response.StatusCode}.");
    }

    private async Task<Dictionary<string, CatalogEntry>> ReadCatalogAsync(CancellationToken ct)
    {
        var path = Path.GetFullPath(options.Value.CatalogPath);
        if (!File.Exists(path)) return new(StringComparer.Ordinal);
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous);
        return await JsonSerializer.DeserializeAsync<Dictionary<string, CatalogEntry>>(stream, JsonOptions, ct)
            ?? new(StringComparer.Ordinal);
    }

    private async Task WriteCatalogAsync(Dictionary<string, CatalogEntry> catalog, CancellationToken ct)
    {
        var path = Path.GetFullPath(options.Value.CatalogPath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        await using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous))
        {
            await JsonSerializer.SerializeAsync(stream, catalog, JsonOptions, ct);
            await stream.FlushAsync(ct);
        }
        File.Move(temporaryPath, path, true);
    }

    private static object SourceFilter(string sourcePath) => new
    {
        must = new[] { new { key = "sourcePath", match = new { value = sourcePath } } }
    };

    private static object OldVersionFilter(string sourcePath, string version) => new
    {
        must = new[] { new { key = "sourcePath", match = new { value = sourcePath } } },
        must_not = new[] { new { key = "documentVersion", match = new { value = version } } }
    };

    private static string CatalogKey(string vectorIndexName, string sourcePath) => vectorIndexName + "\0" + sourcePath;

    private static Guid DeterministicGuid(string value)
    {
        var hash = MD5.HashData(Encoding.UTF8.GetBytes(value));
        return new Guid(hash);
    }

    private sealed record CatalogEntry(string ContentHash, string DocumentVersion, int ChunkCount)
    {
        public IngestedVectorDocument ToResult(string index, SourceText source) =>
            new(index, source.SourceName, source.CanonicalSourcePath, ContentHash, DocumentVersion, ChunkCount);
    }
}

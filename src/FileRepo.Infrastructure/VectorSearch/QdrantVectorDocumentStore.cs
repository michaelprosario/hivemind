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
            var kind = payload.TryGetProperty("kind", out var k) && k.ValueKind == JsonValueKind.String ? k.GetString() ?? "file" : "file";
            string[]? tags = payload.TryGetProperty("tags", out var t) && t.ValueKind == JsonValueKind.Array
                ? t.EnumerateArray().Select(x => x.GetString() ?? "").ToArray()
                : null;
            results.Add(new(
                vectorIndexName,
                payload.TryGetProperty("sourceName", out var sn) ? sn.GetString() ?? "" : "",
                payload.TryGetProperty("sourcePath", out var sp) ? sp.GetString() ?? "" : "",
                payload.GetProperty("chunkIndex").GetInt32(),
                payload.GetProperty("startCharacter").GetInt32(),
                payload.GetProperty("endCharacter").GetInt32(),
                payload.GetProperty("text").GetString() ?? "",
                point.GetProperty("score").GetSingle(),
                kind,
                payload.TryGetProperty("key", out var key) && key.ValueKind == JsonValueKind.String ? key.GetString() : null,
                tags,
                payload.TryGetProperty("author", out var a) && a.ValueKind == JsonValueKind.String ? a.GetString() : null,
                payload.TryGetProperty("sourceUrl", out var su) && su.ValueKind == JsonValueKind.String ? su.GetString() : null));
        }
        return AppResult<IReadOnlyList<VectorSearchHit>>.Ok(results);
    }

    public async Task<AppResult<StoredMemory>> StoreMemoryAsync(
        string vectorIndexName,
        MemoryRecord memory,
        IReadOnlyList<VectorChunk> chunks,
        IReadOnlyList<ReadOnlyMemory<float>> vectors,
        int vectorSize,
        string modelRevision,
        CancellationToken ct)
    {
        var gate = SourceLocks.GetOrAdd($"{vectorIndexName}\0memory\0{memory.Key}", _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct);
        try
        {
            await EnsureCollectionAsync(vectorIndexName, vectorSize, ct);

            var groupId = DeterministicGuid($"memory\n{vectorIndexName}\n{memory.Key}");
            var createdAt = await ReadExistingCreatedAtAsync(vectorIndexName, memory.Key, ct) ?? DateTimeOffset.UtcNow;
            var updatedAt = DateTimeOffset.UtcNow;

            var pointIds = chunks.Select(chunk => DeterministicGuid($"{groupId}\n{chunk.ChunkIndex}").ToString()).ToArray();
            var points = chunks.Select((chunk, index) => new
            {
                id = pointIds[index],
                vector = vectors[index].ToArray(),
                payload = new
                {
                    kind = "memory",
                    key = memory.Key,
                    tags = memory.Tags,
                    author = memory.Author,
                    sourceUrl = memory.SourceUrl,
                    createdAt = createdAt.ToString("O"),
                    updatedAt = updatedAt.ToString("O"),
                    contentHash = memory.ContentHash,
                    isActive = true,
                    chunkIndex = chunk.ChunkIndex,
                    startCharacter = chunk.StartCharacter,
                    endCharacter = chunk.EndCharacter,
                    text = chunk.Text,
                    modelRevision
                }
            }).ToArray();
            await SendSuccessAsync(HttpMethod.Put, $"/collections/{vectorIndexName}/points?wait=true", new { points }, ct);
            await SendSuccessAsync(HttpMethod.Post, $"/collections/{vectorIndexName}/points/delete?wait=true",
                new { filter = StaleMemoryChunkFilter(memory.Key, chunks.Count) }, ct);

            return AppResult<StoredMemory>.Ok(new(
                vectorIndexName, memory.Key, memory.ContentHash, chunks.Count, createdAt, updatedAt));
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<DateTimeOffset?> ReadExistingCreatedAtAsync(string vectorIndexName, string key, CancellationToken ct)
    {
        using var response = await client.PostAsJsonAsync($"/collections/{vectorIndexName}/points/scroll", new
        {
            filter = new { must = new[] { new { key = "key", match = new { value = key } } } },
            limit = 1,
            with_payload = true,
            with_vector = false
        }, JsonOptions, ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        await EnsureSuccessAsync(response, ct);
        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        var points = document.RootElement.GetProperty("result").GetProperty("points");
        foreach (var point in points.EnumerateArray())
        {
            if (point.GetProperty("payload").TryGetProperty("createdAt", out var createdAt) &&
                createdAt.ValueKind == JsonValueKind.String &&
                DateTimeOffset.TryParse(createdAt.GetString(), out var parsed))
                return parsed;
        }
        return null;
    }

    private static object StaleMemoryChunkFilter(string key, int chunkCount) => new
    {
        must = new object[]
        {
            new { key = "key", match = new { value = key } },
            new { key = "chunkIndex", range = new { gte = chunkCount } }
        }
    };

    public async Task<AppResult<IReadOnlyList<VectorIndexInfo>>> ListIndexesAsync(
        bool includeDetails,
        int expectedVectorSize,
        CancellationToken ct)
    {
        using var response = await client.GetAsync("/collections", ct);
        await EnsureSuccessAsync(response, ct);
        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        var names = document.RootElement.GetProperty("result").GetProperty("collections")
            .EnumerateArray()
            .Select(c => c.GetProperty("name").GetString() ?? "")
            .Where(n => n.Length > 0)
            .Order(StringComparer.Ordinal)
            .ToArray();

        if (!includeDetails)
            return AppResult<IReadOnlyList<VectorIndexInfo>>.Ok(
                names.Select(n => new VectorIndexInfo(n, null, null, null, null, null)).ToArray());

        var details = new VectorIndexInfo?[names.Length];
        await Parallel.ForEachAsync(
            Enumerable.Range(0, names.Length),
            new ParallelOptions { MaxDegreeOfParallelism = 4, CancellationToken = ct },
            async (i, token) => details[i] = await ReadIndexInfoAsync(names[i], expectedVectorSize, token));
        return AppResult<IReadOnlyList<VectorIndexInfo>>.Ok(details.Where(d => d is not null).Select(d => d!).ToArray());
    }

    private async Task<VectorIndexInfo?> ReadIndexInfoAsync(string name, int expectedVectorSize, CancellationToken ct)
    {
        using var response = await client.GetAsync($"/collections/{Uri.EscapeDataString(name)}", ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        await EnsureSuccessAsync(response, ct);
        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        var result = document.RootElement.GetProperty("result");
        var status = result.TryGetProperty("status", out var s) && s.ValueKind == JsonValueKind.String ? s.GetString() : null;
        long? points = result.TryGetProperty("points_count", out var pc) && pc.ValueKind == JsonValueKind.Number ? pc.GetInt64() : null;

        int? size = null;
        string? distance = null;
        if (result.TryGetProperty("config", out var config) &&
            config.TryGetProperty("params", out var prms) &&
            prms.TryGetProperty("vectors", out var vectors) &&
            vectors.ValueKind == JsonValueKind.Object)
        {
            if (vectors.TryGetProperty("size", out var sz) && sz.ValueKind == JsonValueKind.Number) size = sz.GetInt32();
            if (vectors.TryGetProperty("distance", out var d) && d.ValueKind == JsonValueKind.String) distance = d.GetString();
        }
        var compatible = size == expectedVectorSize && string.Equals(distance, "Cosine", StringComparison.OrdinalIgnoreCase);
        return new VectorIndexInfo(name, size, distance, points, status, compatible);
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
        await SendSuccessAsync(HttpMethod.Put, $"/collections/{vectorIndexName}/index", new
        {
            field_name = "key",
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

using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using FileRepo.Core.Common;

namespace FileRepo.Core.VectorSearch;

public sealed partial class VectorDocumentService(
    ISourceTextReader sourceReader,
    ITextChunker chunker,
    ITextEmbeddingProvider embeddingProvider,
    IVectorDocumentStore store,
    VectorSearchValidationRules rules) : IVectorDocumentService
{
    public async Task<AppResult<IngestedVectorDocument>> IngestAsync(
        IngestVectorDocumentCommand command,
        CancellationToken ct)
    {
        var errors = ValidateIndex(command.VectorIndexName);
        if (string.IsNullOrWhiteSpace(command.SourcePath))
            errors.Add(new ValidationError("sourcePath", "Source path is required."));
        if (errors.Count > 0) return AppResult<IngestedVectorDocument>.Invalid(errors);

        var source = await sourceReader.ReadAsync(command.SourcePath, ct);
        if (!source.IsSuccess || source.Value is null) return CopyFailure<SourceText, IngestedVectorDocument>(source);

        var chunks = chunker.Chunk(source.Value.Content);
        if (chunks.Count == 0)
            return AppResult<IngestedVectorDocument>.Invalid([new("sourcePath", "Source file does not contain indexable text.")]);

        var vectors = await embeddingProvider.EmbedDocumentsAsync(chunks.Select(x => x.Text).ToArray(), ct);
        if (vectors.Count != chunks.Count)
            throw new InvalidOperationException("Embedding provider returned a vector count that does not match the chunk count.");

        return await store.IngestAsync(command.VectorIndexName, source.Value, chunks, vectors,
            embeddingProvider.VectorSize, embeddingProvider.ModelRevision, ct);
    }

    public async Task<AppResult<IReadOnlyList<VectorSearchHit>>> SearchAsync(
        SearchVectorDocumentsQuery query,
        CancellationToken ct)
    {
        var errors = ValidateIndex(query.VectorIndexName);
        if (string.IsNullOrWhiteSpace(query.QueryText))
            errors.Add(new ValidationError("queryText", "Query text is required."));
        else if (query.QueryText.Length > rules.MaxQueryLength)
            errors.Add(new ValidationError("queryText", $"Query text must be at most {rules.MaxQueryLength} characters."));
        if (query.Take is < 1 or > 50)
            errors.Add(new ValidationError("take", "Take must be between 1 and 50."));
        if (errors.Count > 0) return AppResult<IReadOnlyList<VectorSearchHit>>.Invalid(errors);

        var vector = await embeddingProvider.EmbedQueryAsync(query.QueryText.Trim(), ct);
        if (vector.Length != embeddingProvider.VectorSize)
            throw new InvalidOperationException("Embedding provider returned a vector with an unexpected dimension.");
        return await store.SearchAsync(query.VectorIndexName, vector, query.Take, ct);
    }

    public Task<AppResult<IReadOnlyList<VectorIndexInfo>>> ListIndexesAsync(
        ListVectorIndexesQuery query,
        CancellationToken ct) =>
        store.ListIndexesAsync(query.IncludeDetails, embeddingProvider.VectorSize, ct);

    public Task<AppResult<VectorIndexSchema>> InspectIndexSchemaAsync(
        InspectVectorIndexSchemaQuery query,
        CancellationToken ct)
    {
        var errors = ValidateIndex(query.CollectionName, "collectionName");
        if (errors.Count > 0) return Task.FromResult(AppResult<VectorIndexSchema>.Invalid(errors));
        return store.InspectIndexSchemaAsync(query.CollectionName, ct);
    }

    public Task<AppResult<VectorIndexInfo>> CreateIndexAsync(
        CreateVectorIndexCommand command,
        CancellationToken ct)
    {
        var errors = ValidateIndex(command.Name, "name");
        if (errors.Count > 0) return Task.FromResult(AppResult<VectorIndexInfo>.Invalid(errors));
        return store.CreateIndexAsync(command.Name, embeddingProvider.VectorSize, ct);
    }

    public async Task<AppResult<StoredMemory>> StoreMemoryAsync(
        StoreMemoryCommand command,
        CancellationToken ct)
    {
        var errors = ValidateIndex(command.VectorIndexName);
        if (string.IsNullOrWhiteSpace(command.Text))
            errors.Add(new ValidationError("text", "Text is required."));
        else if (command.Text.Length > rules.MaxMemoryTextLength)
            errors.Add(new ValidationError("text", $"Text must be at most {rules.MaxMemoryTextLength} characters."));

        if (command.Key is not null && !MemoryKeyPattern().IsMatch(command.Key))
            errors.Add(new ValidationError("key", "Key must match ^[A-Za-z0-9][A-Za-z0-9_.:-]{0,127}$."));

        var tags = Array.Empty<string>();
        if (command.Tags is { Count: > 0 })
        {
            if (command.Tags.Count > rules.MaxTagCount)
                errors.Add(new ValidationError("tags", $"At most {rules.MaxTagCount} tags are allowed."));
            tags = command.Tags.Select(t => t?.Trim() ?? "").Where(t => t.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            if (tags.Any(t => t.Length > rules.MaxTagLength))
                errors.Add(new ValidationError("tags", $"Each tag must be at most {rules.MaxTagLength} characters."));
        }

        if (!string.IsNullOrWhiteSpace(command.Author) && command.Author.Length > rules.MaxAuthorLength)
            errors.Add(new ValidationError("author", $"Author must be at most {rules.MaxAuthorLength} characters."));

        if (!string.IsNullOrWhiteSpace(command.SourceUrl) &&
            !Uri.TryCreate(command.SourceUrl, UriKind.Absolute, out _))
            errors.Add(new ValidationError("sourceUrl", "Source URL must be an absolute URL."));

        if (errors.Count > 0) return AppResult<StoredMemory>.Invalid(errors);

        var chunks = chunker.Chunk(command.Text);
        if (chunks.Count == 0)
            return AppResult<StoredMemory>.Invalid([new("text", "Text does not contain indexable content.")]);

        var vectors = await embeddingProvider.EmbedDocumentsAsync(chunks.Select(x => x.Text).ToArray(), ct);
        if (vectors.Count != chunks.Count)
            throw new InvalidOperationException("Embedding provider returned a vector count that does not match the chunk count.");

        var contentHash = $"sha256:{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(command.Text))).ToLowerInvariant()}";
        var key = string.IsNullOrWhiteSpace(command.Key) ? Guid.NewGuid().ToString("N") : command.Key;
        var memory = new MemoryRecord(key, command.Key is not null, contentHash, tags,
            string.IsNullOrWhiteSpace(command.Author) ? null : command.Author.Trim(),
            string.IsNullOrWhiteSpace(command.SourceUrl) ? null : command.SourceUrl.Trim());

        return await store.StoreMemoryAsync(command.VectorIndexName, memory, chunks, vectors,
            embeddingProvider.VectorSize, embeddingProvider.ModelRevision, ct);
    }

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9_.:-]{0,127}$", RegexOptions.CultureInvariant)]
    private static partial Regex MemoryKeyPattern();

    private static List<ValidationError> ValidateIndex(string indexName, string field = "vectorIndexName")
    {
        var errors = new List<ValidationError>();
        if (string.IsNullOrWhiteSpace(indexName) || !IndexNamePattern().IsMatch(indexName))
            errors.Add(new ValidationError(field, "Vector index name must match ^[a-z0-9][a-z0-9_-]{0,62}$."));
        return errors;
    }

    private static AppResult<TTo> CopyFailure<TFrom, TTo>(AppResult<TFrom> result) =>
        result.Status switch
        {
            ResultStatus.Invalid => AppResult<TTo>.Invalid(result.ValidationErrors),
            ResultStatus.NotFound => AppResult<TTo>.NotFound(result.Messages.FirstOrDefault() ?? "Resource was not found."),
            ResultStatus.Conflict => AppResult<TTo>.Conflict(result.Messages.FirstOrDefault() ?? "Resource conflicts with an existing resource."),
            ResultStatus.PayloadTooLarge => AppResult<TTo>.PayloadTooLarge(result.Messages.FirstOrDefault() ?? "Resource is too large."),
            _ => AppResult<TTo>.Error(result.Messages.FirstOrDefault() ?? "The operation failed.")
        };

    [GeneratedRegex("^[a-z0-9][a-z0-9_-]{0,62}$", RegexOptions.CultureInvariant)]
    private static partial Regex IndexNamePattern();
}

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

    private static List<ValidationError> ValidateIndex(string indexName)
    {
        var errors = new List<ValidationError>();
        if (string.IsNullOrWhiteSpace(indexName) || !IndexNamePattern().IsMatch(indexName))
            errors.Add(new ValidationError("vectorIndexName", "Vector index name must match ^[a-z0-9][a-z0-9_-]{0,62}$."));
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

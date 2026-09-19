using FileRepo.Core.Common;

namespace FileRepo.Core.VectorSearch;

public interface IVectorDocumentService
{
    Task<AppResult<IngestedVectorDocument>> IngestAsync(
        IngestVectorDocumentCommand command,
        CancellationToken ct);

    Task<AppResult<IReadOnlyList<VectorSearchHit>>> SearchAsync(
        SearchVectorDocumentsQuery query,
        CancellationToken ct);
}

public interface ISourceTextReader
{
    Task<AppResult<SourceText>> ReadAsync(string sourcePath, CancellationToken ct);
}

public interface ITextChunker
{
    IReadOnlyList<VectorChunk> Chunk(string text);
}

public interface ITextEmbeddingProvider
{
    int VectorSize { get; }
    string ModelRevision { get; }
    Task<IReadOnlyList<ReadOnlyMemory<float>>> EmbedDocumentsAsync(
        IReadOnlyList<string> texts,
        CancellationToken ct);
    Task<ReadOnlyMemory<float>> EmbedQueryAsync(string queryText, CancellationToken ct);
}

public interface IVectorDocumentStore
{
    Task<AppResult<IngestedVectorDocument>> IngestAsync(
        string vectorIndexName,
        SourceText source,
        IReadOnlyList<VectorChunk> chunks,
        IReadOnlyList<ReadOnlyMemory<float>> vectors,
        int vectorSize,
        string modelRevision,
        CancellationToken ct);

    Task<AppResult<IReadOnlyList<VectorSearchHit>>> SearchAsync(
        string vectorIndexName,
        ReadOnlyMemory<float> queryVector,
        int take,
        CancellationToken ct);
}

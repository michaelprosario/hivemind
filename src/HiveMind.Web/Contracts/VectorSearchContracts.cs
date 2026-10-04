using HiveMind.Core.VectorSearch;

namespace HiveMind.Web.Contracts;

public sealed record IngestVectorDocumentRequest(string VectorIndexName, string SourcePath);
public sealed record IngestVectorDocumentDirectoryRequest(
    string VectorIndexName,
    string DirectoryPath,
    bool IncludeSubdirectories = false);
public sealed record SearchVectorDocumentsRequest(string VectorIndexName, string QueryText, int Take = 5);
public sealed record StoreMemoryRequest(
    string VectorIndexName,
    string Text,
    string? Key = null,
    IReadOnlyList<string>? Tags = null,
    string? Author = null,
    string? SourceUrl = null);

public sealed record IngestVectorDocumentResponse(
    string VectorIndexName,
    string SourceName,
    string CanonicalSourcePath,
    string ContentHash,
    string DocumentVersion,
    int ChunkCount)
{
    public static IngestVectorDocumentResponse From(IngestedVectorDocument document) => new(
        document.VectorIndexName, document.SourceName, document.CanonicalSourcePath,
        document.ContentHash, document.DocumentVersion, document.ChunkCount);
}

public sealed record VectorSearchHitResponse(
    string VectorIndexName,
    string SourceName,
    string CanonicalSourcePath,
    int ChunkIndex,
    int StartCharacter,
    int EndCharacter,
    string Text,
    float Score,
    string Kind,
    string? Key,
    IReadOnlyList<string>? Tags,
    string? Author,
    string? SourceUrl)
{
    public static VectorSearchHitResponse From(VectorSearchHit hit) => new(
        hit.VectorIndexName, hit.SourceName, hit.CanonicalSourcePath, hit.ChunkIndex,
        hit.StartCharacter, hit.EndCharacter, hit.Text, hit.Score, hit.Kind, hit.Key, hit.Tags, hit.Author, hit.SourceUrl);
}

public sealed record SearchVectorDocumentsResponse(IReadOnlyList<VectorSearchHitResponse> Results);

public sealed record DirectoryFileOutcomeResponse(
    string SourcePath,
    DirectoryFileIngestStatus Status,
    IngestVectorDocumentResponse? Document,
    string? Reason)
{
    public static DirectoryFileOutcomeResponse From(DirectoryFileOutcome outcome) => new(
        outcome.SourcePath,
        outcome.Status,
        outcome.Document is null ? null : IngestVectorDocumentResponse.From(outcome.Document),
        outcome.Reason);
}

public sealed record IngestedVectorDirectoryResponse(
    string VectorIndexName,
    string DirectoryPath,
    int FilesDiscovered,
    int IngestedCount,
    int SkippedCount,
    int FailedCount,
    IReadOnlyList<DirectoryFileOutcomeResponse> Files)
{
    public static IngestedVectorDirectoryResponse From(IngestedVectorDirectory result) => new(
        result.VectorIndexName,
        result.DirectoryPath,
        result.FilesDiscovered,
        result.IngestedCount,
        result.SkippedCount,
        result.FailedCount,
        result.Files.Select(DirectoryFileOutcomeResponse.From).ToArray());
}

public sealed record VectorIndexResponse(
    string Name,
    int? VectorSize,
    string? Distance,
    long? PointsCount,
    string? Status,
    bool? IsCompatible)
{
    public static VectorIndexResponse From(VectorIndexInfo info) => new(
        info.Name, info.VectorSize, info.Distance, info.PointsCount, info.Status, info.IsCompatible);
}

public sealed record ListVectorIndexesResponse(IReadOnlyList<VectorIndexResponse> Indexes);

public sealed record StoreMemoryResponse(
    string VectorIndexName,
    string Key,
    string ContentHash,
    int ChunkCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public static StoreMemoryResponse From(StoredMemory memory) => new(
        memory.VectorIndexName, memory.Key, memory.ContentHash, memory.ChunkCount, memory.CreatedAt, memory.UpdatedAt);
}

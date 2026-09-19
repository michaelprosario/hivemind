using FileRepo.Core.VectorSearch;

namespace FileRepo.Web.Contracts;

public sealed record IngestVectorDocumentRequest(string VectorIndexName, string SourcePath);
public sealed record SearchVectorDocumentsRequest(string VectorIndexName, string QueryText, int Take = 5);

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
    float Score)
{
    public static VectorSearchHitResponse From(VectorSearchHit hit) => new(
        hit.VectorIndexName, hit.SourceName, hit.CanonicalSourcePath, hit.ChunkIndex,
        hit.StartCharacter, hit.EndCharacter, hit.Text, hit.Score);
}

public sealed record SearchVectorDocumentsResponse(IReadOnlyList<VectorSearchHitResponse> Results);

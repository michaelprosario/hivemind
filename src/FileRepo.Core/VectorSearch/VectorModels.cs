namespace FileRepo.Core.VectorSearch;

public sealed record SourceText(
    string SourceName,
    string CanonicalSourcePath,
    string Content,
    string ContentHash);

public sealed record VectorChunk(
    int ChunkIndex,
    int StartCharacter,
    int EndCharacter,
    string Text);

public sealed record IngestVectorDocumentCommand(
    string VectorIndexName,
    string SourcePath);

public sealed record IngestVectorDocumentDirectoryCommand(
    string VectorIndexName,
    string DirectoryPath,
    bool IncludeSubdirectories = false);

public enum DirectoryFileIngestStatus { Ingested, Skipped, Failed }

public sealed record DirectoryFileOutcome(
    string SourcePath,
    DirectoryFileIngestStatus Status,
    IngestedVectorDocument? Document,
    string? Reason);

public sealed record IngestedVectorDirectory(
    string VectorIndexName,
    string DirectoryPath,
    int FilesDiscovered,
    int IngestedCount,
    int SkippedCount,
    int FailedCount,
    IReadOnlyList<DirectoryFileOutcome> Files);

public sealed record SearchVectorDocumentsQuery(
    string VectorIndexName,
    string QueryText,
    int Take = 5);

public sealed record IngestedVectorDocument(
    string VectorIndexName,
    string SourceName,
    string CanonicalSourcePath,
    string ContentHash,
    string DocumentVersion,
    int ChunkCount);

public sealed record VectorSearchHit(
    string VectorIndexName,
    string SourceName,
    string CanonicalSourcePath,
    int ChunkIndex,
    int StartCharacter,
    int EndCharacter,
    string Text,
    float Score);

public sealed class VectorSearchValidationRules
{
    public int ChunkSizeCharacters { get; init; } = 1_600;
    public int ChunkOverlapCharacters { get; init; } = 240;
    public int MaxQueryLength { get; init; } = 8_000;
}

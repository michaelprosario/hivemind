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

public sealed record StoreMemoryCommand(
    string VectorIndexName,
    string Text,
    string? Key,
    IReadOnlyList<string>? Tags,
    string? Author,
    string? SourceUrl);

public sealed record StoredMemory(
    string VectorIndexName,
    string Key,
    string ContentHash,
    int ChunkCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

/// <summary>Normalized, validated memory metadata passed from the Core service down to the store port.</summary>
public sealed record MemoryRecord(
    string Key,
    bool IsCallerSuppliedKey,
    string ContentHash,
    IReadOnlyList<string> Tags,
    string? Author,
    string? SourceUrl);

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
    float Score,
    string Kind = "file",
    string? Key = null,
    IReadOnlyList<string>? Tags = null,
    string? Author = null,
    string? SourceUrl = null);

public sealed class VectorSearchValidationRules
{
    public int ChunkSizeCharacters { get; init; } = 1_600;
    public int ChunkOverlapCharacters { get; init; } = 240;
    public int MaxQueryLength { get; init; } = 8_000;
    public int MaxMemoryTextLength { get; init; } = 200_000;
    public int MaxTagCount { get; init; } = 32;
    public int MaxTagLength { get; init; } = 64;
    public int MaxAuthorLength { get; init; } = 256;
}

public sealed record ListVectorIndexesQuery(bool IncludeDetails = true);

public sealed record VectorIndexInfo(
    string Name,
    int? VectorSize,
    string? Distance,
    long? PointsCount,
    string? Status,
    bool? IsCompatible);

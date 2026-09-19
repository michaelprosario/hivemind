using System.ComponentModel.DataAnnotations;

namespace FileRepo.Infrastructure.VectorSearch;

public sealed class VectorSearchOptions
{
    [Required] public string AllowedSourceRoot { get; set; } = "";
    [Range(1, long.MaxValue)] public long MaxSourceFileSizeBytes { get; set; } = 10_485_760;
    [MinLength(1)] public string[] SupportedExtensions { get; set; } = [".txt", ".md"];
    [Range(1, 100_000)] public int ChunkSizeCharacters { get; set; } = 1_600;
    [Range(0, 99_999)] public int ChunkOverlapCharacters { get; set; } = 240;
    [Range(1, 128)] public int EmbeddingBatchSize { get; set; } = 32;
    [Required] public string ModelDirectory { get; set; } = "";
    [Required] public string ModelRevision { get; set; } = "";
    [Required] public string CatalogPath { get; set; } = "./data/vector-search/catalog.json";
    [Required] public string QdrantEndpoint { get; set; } = "http://localhost:6333";
    public string? QdrantApiKey { get; set; }
}

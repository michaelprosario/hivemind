using FileRepo.Core.Files;
using FileRepo.Infrastructure.FileSystem;
using FileRepo.Core.VectorSearch;
using FileRepo.Infrastructure.VectorSearch;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
namespace FileRepo.Infrastructure;
public static class DependencyInjection
{
    public static IServiceCollection AddFileRepositoryInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<FileSystemRepositoryOptions>().BindConfiguration("FileRepository").ValidateDataAnnotations().ValidateOnStart();
        services.AddSingleton<IFileRepository, FileSystemFileRepository>();
        return services;
    }

    public static IServiceCollection AddVectorSearchInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<VectorSearchOptions>()
            .BindConfiguration("VectorSearch")
            .ValidateDataAnnotations()
            .Validate(x => x.ChunkOverlapCharacters < x.ChunkSizeCharacters,
                "Chunk overlap must be smaller than chunk size.")
            .ValidateOnStart();
        services.AddSingleton(sp =>
        {
            var options = sp.GetRequiredService<IOptions<VectorSearchOptions>>().Value;
            var client = new HttpClient { BaseAddress = new Uri(options.QdrantEndpoint.TrimEnd('/') + "/") };
            if (!string.IsNullOrWhiteSpace(options.QdrantApiKey))
                client.DefaultRequestHeaders.Add("api-key", options.QdrantApiKey);
            return client;
        });
        services.AddSingleton<ISourceTextReader, FileSystemSourceTextReader>();
        services.AddSingleton<IDirectoryFileEnumerator, FileSystemDirectoryEnumerator>();
        services.AddSingleton<ITextChunker, TextChunker>();
        services.AddSingleton<ITextEmbeddingProvider, OnnxTextEmbeddingProvider>();
        services.AddSingleton<IVectorDocumentStore, QdrantVectorDocumentStore>();
        services.AddScoped<IDirectoryIngestionService, DirectoryIngestionService>();
        services.AddSingleton(sp =>
        {
            var options = sp.GetRequiredService<IOptions<VectorSearchOptions>>().Value;
            return new VectorSearchValidationRules
            {
                ChunkSizeCharacters = options.ChunkSizeCharacters,
                ChunkOverlapCharacters = options.ChunkOverlapCharacters
            };
        });
        return services;
    }
}

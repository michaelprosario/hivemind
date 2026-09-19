using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using FileRepo.Core.Common;
using FileRepo.Core.VectorSearch;
using ModelContextProtocol.Server;

namespace FileRepo.Mcp.Tools;

/// <summary>
/// Exposes the File Repository's vector search use cases as MCP tools. Each
/// tool delegates directly to the same Core services (<see cref="IVectorDocumentService"/>,
/// <see cref="IDirectoryIngestionService"/>) used by <c>VectorDocumentsController</c>,
/// so ingestion and search behavior is identical whether the caller is the HTTP
/// API or an MCP client such as Claude Desktop.
/// </summary>
[McpServerToolType]
public static class VectorSearchTools
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    [McpServerTool, Description(
        "Indexes a single permitted local text file into the specified Qdrant vector index so its content " +
        "becomes semantically searchable. The source path must resolve inside the server's configured " +
        "VectorSearch:AllowedSourceRoot and have a supported extension.")]
    public static async Task<string> IngestFile(
        IVectorDocumentService vectorDocumentService,
        [Description("Vector index (Qdrant collection) name, e.g. 'docs_v1'. Must match ^[a-z0-9][a-z0-9_-]{0,62}$.")]
        string vectorIndexName,
        [Description("Path to the source text file, absolute or relative to the allowed root.")]
        string sourcePath,
        CancellationToken cancellationToken = default)
    {
        var result = await vectorDocumentService.IngestAsync(
            new(vectorIndexName, sourcePath), cancellationToken);
        return ToJson(result);
    }

    [McpServerTool, Description(
        "Indexes every supported file directly under a permitted local directory into the specified Qdrant " +
        "vector index in one call. Reports each file as Ingested, Skipped (unsupported extension), or Failed, " +
        "so one bad file never blocks the rest of the folder.")]
    public static async Task<string> IngestDirectory(
        IDirectoryIngestionService directoryIngestionService,
        [Description("Vector index (Qdrant collection) name, e.g. 'docs_v1'.")]
        string vectorIndexName,
        [Description("Path to the source directory, absolute or relative to the allowed root.")]
        string directoryPath,
        [Description("Set true to also ingest files in subdirectories. Defaults to false.")]
        bool includeSubdirectories = false,
        CancellationToken cancellationToken = default)
    {
        var result = await directoryIngestionService.IngestDirectoryAsync(
            new(vectorIndexName, directoryPath, includeSubdirectories), cancellationToken);
        return ToJson(result);
    }

    [McpServerTool, Description(
        "Finds the most semantically similar indexed text chunks in the specified Qdrant vector index for a " +
        "natural-language query, returning each match's source file, location, and similarity score.")]
    public static async Task<string> Search(
        IVectorDocumentService vectorDocumentService,
        [Description("Vector index (Qdrant collection) name to search, e.g. 'docs_v1'.")]
        string vectorIndexName,
        [Description("Natural-language query text.")]
        string queryText,
        [Description("Number of results to return (1-50). Defaults to 5.")]
        int take = 5,
        CancellationToken cancellationToken = default)
    {
        var result = await vectorDocumentService.SearchAsync(
            new(vectorIndexName, queryText, take), cancellationToken);
        return ToJson(result);
    }

    private static string ToJson<T>(AppResult<T> result)
    {
        object payload = result.IsSuccess
            ? new { success = true, data = result.Value }
            : new
            {
                success = false,
                status = result.Status.ToString(),
                messages = result.Messages,
                validationErrors = result.ValidationErrors
            };
        return JsonSerializer.Serialize(payload, JsonOptions);
    }
}

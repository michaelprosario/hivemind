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
        "Finds the most semantically similar indexed text chunks in the specified Qdrant vector index for a " +
        "natural-language query, returning each match's source file (or stored memory key), location, and " +
        "similarity score. Use list_indexes to discover available index names.")]
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

    [McpServerTool, Description(
        "Lists the vector indexes (Qdrant collections) available in the Qdrant instance, with vector size, " +
        "distance metric, point count, status, and whether each is compatible with this server's embedding model.")]
    public static async Task<string> ListIndexes(
        IVectorDocumentService vectorDocumentService,
        [Description("Include per-index details. Set false for names only. Defaults to true.")]
        bool includeDetails = true,
        CancellationToken cancellationToken = default)
    {
        var result = await vectorDocumentService.ListIndexesAsync(new(includeDetails), cancellationToken);
        return ToJson(result);
    }

    [McpServerTool, Description(
        "Stores text (notes, code snippets, user preferences, task progress) as agent memory in a named vector " +
        "index so it can later be found via the search tool. Provide an idempotent key to update/overwrite an " +
        "existing memory; omit it to always create a new entry. The index is created automatically if it does " +
        "not already exist.")]
    public static async Task<string> StoreMemory(
        IVectorDocumentService vectorDocumentService,
        [Description("Vector index (Qdrant collection) name to store into, e.g. 'agent_memory'.")]
        string vectorIndexName,
        [Description("The text content to store.")]
        string text,
        [Description("Optional idempotent key/id. Storing again with the same key replaces the prior content and metadata.")]
        string? key = null,
        [Description("Optional tags for filtering/organization.")]
        string[]? tags = null,
        [Description("Optional author identifier.")]
        string? author = null,
        [Description("Optional source URL this memory was derived from.")]
        string? sourceUrl = null,
        CancellationToken cancellationToken = default)
    {
        var result = await vectorDocumentService.StoreMemoryAsync(
            new(vectorIndexName, text, key, tags, author, sourceUrl), cancellationToken);
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

using FileRepo.Core.VectorSearch;
using FileRepo.Web.Contracts;
using FileRepo.Web.Extensions;
using Microsoft.AspNetCore.Mvc;

namespace FileRepo.Web.Controllers;

[ApiController, Route("api/vector-documents")]
public sealed class VectorDocumentsController(
    IVectorDocumentService service,
    IDirectoryIngestionService directoryService) : ControllerBase
{
    /// <summary>Indexes a permitted local text file in the specified vector index.</summary>
    [HttpPost("ingest"), ProducesResponseType(typeof(IngestVectorDocumentResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Ingest(IngestVectorDocumentRequest request, CancellationToken ct)
    {
        var result = await service.IngestAsync(new(request.VectorIndexName, request.SourcePath), ct);
        return result.ToActionResult(this, value => Ok(IngestVectorDocumentResponse.From(value)));
    }

    /// <summary>Indexes all files in a permitted local directory into the specified vector index.</summary>
    [HttpPost("ingest-directory"), ProducesResponseType(typeof(IngestedVectorDirectoryResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> IngestDirectory(
        IngestVectorDocumentDirectoryRequest request, CancellationToken ct)
    {
        var result = await directoryService.IngestDirectoryAsync(
            new(request.VectorIndexName, request.DirectoryPath, request.IncludeSubdirectories), ct);
        return result.ToActionResult(this, value => Ok(IngestedVectorDirectoryResponse.From(value)));
    }

    /// <summary>Lists the vector indexes (Qdrant collections) in the Qdrant instance.</summary>
    [HttpGet("indexes"), ProducesResponseType(typeof(ListVectorIndexesResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListIndexes([FromQuery] bool includeDetails = true, CancellationToken ct = default)
    {
        var result = await service.ListIndexesAsync(new(includeDetails), ct);
        return result.ToActionResult(this,
            indexes => Ok(new ListVectorIndexesResponse(indexes.Select(VectorIndexResponse.From).ToArray())));
    }

    /// <summary>Finds semantically similar text segments in the specified vector index.</summary>
    [HttpPost("search"), ProducesResponseType(typeof(SearchVectorDocumentsResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Search(SearchVectorDocumentsRequest request, CancellationToken ct)
    {
        var result = await service.SearchAsync(new(request.VectorIndexName, request.QueryText, request.Take), ct);
        return result.ToActionResult(this,
            hits => Ok(new SearchVectorDocumentsResponse(hits.Select(VectorSearchHitResponse.From).ToArray())));
    }
}

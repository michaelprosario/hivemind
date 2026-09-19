using FileRepo.Core.VectorSearch;
using FileRepo.Web.Contracts;
using FileRepo.Web.Extensions;
using Microsoft.AspNetCore.Mvc;

namespace FileRepo.Web.Controllers;

[ApiController, Route("api/vector-documents")]
public sealed class VectorDocumentsController(IVectorDocumentService service) : ControllerBase
{
    /// <summary>Indexes a permitted local text file in the specified vector index.</summary>
    [HttpPost("ingest"), ProducesResponseType(typeof(IngestVectorDocumentResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Ingest(IngestVectorDocumentRequest request, CancellationToken ct)
    {
        var result = await service.IngestAsync(new(request.VectorIndexName, request.SourcePath), ct);
        return result.ToActionResult(this, value => Ok(IngestVectorDocumentResponse.From(value)));
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

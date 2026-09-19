using FileRepo.Core.Files;
using FileRepo.Core.Common;
using FileRepo.Web.Contracts;
using FileRepo.Web.Extensions;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.AspNetCore.Mvc;
namespace FileRepo.Web.Controllers;
[ApiController, Route("api/files")]
public sealed class FilesController(IFileService service) : ControllerBase
{
    [HttpPost("add"), Consumes("multipart/form-data"), ProducesResponseType(typeof(FileSummaryResponse), 201)]
    public async Task<IActionResult> Add([FromForm] UploadFileRequest request, CancellationToken ct)
    {
        if (request.File is null) return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]> { ["file"] = ["A file is required."] }));
        var name = string.IsNullOrWhiteSpace(request.FileName) ? request.File.FileName : request.FileName;
        await using var stream = request.File.OpenReadStream(); var result = await service.AddAsync(new(name, stream, request.File.Length), ct);
        return result.ToActionResult(this, x => CreatedAtAction(nameof(Get), new { }, FileSummaryResponse.From(x)));
    }
    [HttpPost("update"), Consumes("multipart/form-data"), ProducesResponseType(typeof(FileSummaryResponse), 200)]
    public async Task<IActionResult> Update([FromForm] UploadFileRequest request, CancellationToken ct)
    {
        if (request.File is null || string.IsNullOrWhiteSpace(request.FileName)) return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]> { ["fileName"] = ["File name and file are required."] }));
        await using var stream = request.File.OpenReadStream(); var result = await service.UpdateAsync(new(request.FileName, stream, request.File.Length), ct); return result.ToActionResult(this, x => Ok(FileSummaryResponse.From(x)));
    }
    [HttpPost("delete"), ProducesResponseType(204)]
    public async Task<IActionResult> Delete(FileNameRequest request, CancellationToken ct) => (await service.DeleteAsync(new(request.FileName), ct)).ToActionResult(this, 204);
    [HttpPost("get"), Produces("application/octet-stream")]
    public async Task<IActionResult> Get(FileNameRequest request, CancellationToken ct)
    {
        var result = await service.GetAsync(new(request.FileName), ct); if (!result.IsSuccess || result.Value is null) return ((AppResult)result).ToActionResult(this);
        new FileExtensionContentTypeProvider().TryGetContentType(result.Value.Info.Name, out var type); return File(result.Value.Content, type ?? "application/octet-stream", result.Value.Info.Name);
    }
    [HttpPost("list"), ProducesResponseType(typeof(ListFilesResponse), 200)]
    public async Task<IActionResult> List(ListFilesRequest request, CancellationToken ct)
    {
        var result = await service.ListAsync(new(request.NamePrefix, request.Skip, request.Take), ct); return result.ToActionResult(this, x => Ok(new ListFilesResponse(x.Items.Select(FileSummaryResponse.From).ToArray(), x.TotalCount)));
    }
}

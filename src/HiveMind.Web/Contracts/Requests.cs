namespace HiveMind.Web.Contracts;
public sealed class UploadFileRequest { public IFormFile? File { get; set; } public string? FileName { get; set; } }
public sealed record FileNameRequest(string FileName);
public sealed record ListFilesRequest(string? NamePrefix = null, int Skip = 0, int Take = 100);

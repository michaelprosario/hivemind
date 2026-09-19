namespace FileRepo.Core.Files;
public sealed record GetFileQuery(string FileName);
public sealed record ListFilesQuery(string? NamePrefix = null, int Skip = 0, int Take = 100);

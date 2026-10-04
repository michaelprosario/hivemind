using HiveMind.Core.Files;
namespace HiveMind.Web.Contracts;
public sealed record FileSummaryResponse(string Name, long SizeBytes, DateTime CreatedUtc, DateTime ModifiedUtc)
{ public static FileSummaryResponse From(FileSummary x) => new(x.Name, x.SizeBytes, x.CreatedUtc, x.ModifiedUtc); }
public sealed record ListFilesResponse(IReadOnlyList<FileSummaryResponse> Items, int TotalCount);

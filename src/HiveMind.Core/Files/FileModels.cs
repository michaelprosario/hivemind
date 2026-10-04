namespace HiveMind.Core.Files;
public sealed record FileSummary(string Name, long SizeBytes, DateTime CreatedUtc, DateTime ModifiedUtc);
public sealed record FileContent(FileSummary Info, Stream Content);
public sealed class FileValidationRules { public long MaxFileSizeBytes { get; set; } = 10_485_760; public int MaxNameLength { get; set; } = 255; }

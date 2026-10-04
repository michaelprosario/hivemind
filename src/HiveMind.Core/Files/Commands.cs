namespace HiveMind.Core.Files;
public sealed record AddFileCommand(string FileName, Stream Content, long SizeBytes);
public sealed record UpdateFileCommand(string FileName, Stream Content, long SizeBytes);
public sealed record DeleteFileCommand(string FileName);

using System.Security.Cryptography;
using System.Text;
using FileRepo.Core.Common;
using FileRepo.Core.VectorSearch;
using Microsoft.Extensions.Options;

namespace FileRepo.Infrastructure.VectorSearch;

public sealed class FileSystemSourceTextReader(IOptions<VectorSearchOptions> options) : ISourceTextReader
{
    public async Task<AppResult<SourceText>> ReadAsync(string sourcePath, CancellationToken ct)
    {
        var settings = options.Value;
        var root = Path.GetFullPath(settings.AllowedSourceRoot);
        var fullPath = Path.GetFullPath(Path.IsPathRooted(sourcePath)
            ? sourcePath
            : Path.Combine(root, sourcePath));
        if (!fullPath.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            && !string.Equals(fullPath, root, StringComparison.Ordinal))
            return AppResult<SourceText>.Invalid([new("sourcePath", "Source path must be inside the configured allowed root.")]);
        if (!File.Exists(fullPath)) return AppResult<SourceText>.NotFound("Source file was not found.");
        if (!settings.SupportedExtensions.Contains(Path.GetExtension(fullPath), StringComparer.OrdinalIgnoreCase))
            return AppResult<SourceText>.Invalid([new("sourcePath", "Source file extension is not supported.")]);

        var info = new FileInfo(fullPath);
        if (info.Length > settings.MaxSourceFileSizeBytes)
            return AppResult<SourceText>.PayloadTooLarge($"Source file exceeds the maximum size of {settings.MaxSourceFileSizeBytes} bytes.");

        byte[] bytes;
        await using (var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read,
                         65_536, FileOptions.Asynchronous | FileOptions.SequentialScan))
        {
            using var memory = new MemoryStream();
            await stream.CopyToAsync(memory, ct);
            bytes = memory.ToArray();
        }

        var encoding = DetectEncoding(bytes);
        string content;
        try { content = encoding.GetString(bytes); }
        catch (DecoderFallbackException)
        {
            return AppResult<SourceText>.Invalid([new("sourcePath", "Source file is not valid UTF-8 or UTF-16 text.")]);
        }

        var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        return AppResult<SourceText>.Ok(new(Path.GetFileName(fullPath), fullPath, content, $"sha256:{hash}"));
    }

    private static Encoding DetectEncoding(byte[] bytes) =>
        bytes.Length >= 2 && bytes[0] == 0xff && bytes[1] == 0xfe ? Encoding.Unicode :
        bytes.Length >= 2 && bytes[0] == 0xfe && bytes[1] == 0xff ? Encoding.BigEndianUnicode :
        new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
}

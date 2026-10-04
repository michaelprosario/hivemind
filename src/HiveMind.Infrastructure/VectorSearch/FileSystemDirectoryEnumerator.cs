using HiveMind.Core.Common;
using HiveMind.Core.VectorSearch;
using Microsoft.Extensions.Options;

namespace HiveMind.Infrastructure.VectorSearch;

public sealed class FileSystemDirectoryEnumerator(
    IOptions<VectorSearchOptions> options) : IDirectoryFileEnumerator
{
    public Task<AppResult<IReadOnlyList<string>>> EnumerateAsync(
        string directoryPath,
        bool includeSubdirectories,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(directoryPath))
            return Task.FromResult(AppResult<IReadOnlyList<string>>.Invalid(
                [new("directoryPath", "Directory path is required.")]));

        var root = Path.GetFullPath(settings.AllowedSourceRoot);
        var fullPath = Path.GetFullPath(Path.IsPathRooted(directoryPath)
            ? directoryPath
            : Path.Combine(root, directoryPath));
        if (!IsWithinRoot(fullPath, root))
            return Task.FromResult(AppResult<IReadOnlyList<string>>.Invalid(
                [new("directoryPath", "Directory path must be inside the configured allowed root.")]));
        if (!Directory.Exists(fullPath))
            return Task.FromResult(AppResult<IReadOnlyList<string>>.NotFound("Directory was not found."));

        var searchOption = includeSubdirectories
            ? SearchOption.AllDirectories
            : SearchOption.TopDirectoryOnly;
        var files = Directory.EnumerateFiles(fullPath, "*", searchOption)
            .Select(Path.GetFullPath)
            .Where(path => IsWithinRoot(path, root))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        if (files.Length > settings.MaxFilesPerDirectoryIngest)
            return Task.FromResult(AppResult<IReadOnlyList<string>>.Invalid(
                [new("directoryPath",
                    $"Directory contains {files.Length} files, exceeding the maximum of {settings.MaxFilesPerDirectoryIngest}.")]));

        return Task.FromResult(AppResult<IReadOnlyList<string>>.Ok(files));
    }

    private static bool IsWithinRoot(string path, string root) =>
        path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal) ||
        string.Equals(path, root, StringComparison.Ordinal);
}

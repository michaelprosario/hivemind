using System.Text.RegularExpressions;
using FileRepo.Core.Common;

namespace FileRepo.Core.VectorSearch;

public sealed class DirectoryIngestionService(
    IDirectoryFileEnumerator enumerator,
    IVectorDocumentService documentService) : IDirectoryIngestionService
{
    public async Task<AppResult<IngestedVectorDirectory>> IngestDirectoryAsync(
        IngestVectorDocumentDirectoryCommand command,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(command.VectorIndexName) ||
            !IndexNameRegex.IsMatch(command.VectorIndexName))
        {
            return AppResult<IngestedVectorDirectory>.Invalid(
                [new("vectorIndexName", "Vector index name must match ^[a-z0-9][a-z0-9_-]{0,62}$.")]);
        }

        var discovered = await enumerator.EnumerateAsync(
            command.DirectoryPath, command.IncludeSubdirectories, ct);
        if (!discovered.IsSuccess || discovered.Value is null)
            return CopyFailure<IReadOnlyList<string>, IngestedVectorDirectory>(discovered);

        var outcomes = new List<DirectoryFileOutcome>(discovered.Value.Count);
        foreach (var sourcePath in discovered.Value)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var result = await documentService.IngestAsync(
                    new(command.VectorIndexName, sourcePath), ct);
                outcomes.Add(MapOutcome(sourcePath, result));
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception)
            {
                outcomes.Add(new(sourcePath, DirectoryFileIngestStatus.Failed, null,
                    "The file could not be ingested."));
            }
        }

        return AppResult<IngestedVectorDirectory>.Ok(new(
            command.VectorIndexName,
            command.DirectoryPath,
            outcomes.Count,
            outcomes.Count(x => x.Status == DirectoryFileIngestStatus.Ingested),
            outcomes.Count(x => x.Status == DirectoryFileIngestStatus.Skipped),
            outcomes.Count(x => x.Status == DirectoryFileIngestStatus.Failed),
            outcomes));
    }

    private static DirectoryFileOutcome MapOutcome(
        string sourcePath,
        AppResult<IngestedVectorDocument> result)
    {
        if (result.IsSuccess && result.Value is not null)
            return new(sourcePath, DirectoryFileIngestStatus.Ingested, result.Value, null);

        var reason = result.ValidationErrors
            .Select(x => x.Message)
            .Concat(result.Messages)
            .FirstOrDefault() ?? "The file could not be ingested.";
        if (result.Status == ResultStatus.Invalid &&
            reason.Contains("extension", StringComparison.OrdinalIgnoreCase))
        {
            return new(sourcePath, DirectoryFileIngestStatus.Skipped, null, "Extension not supported.");
        }

        return new(sourcePath, DirectoryFileIngestStatus.Failed, null, reason);
    }

    private static AppResult<TTo> CopyFailure<TFrom, TTo>(AppResult<TFrom> result) =>
        result.Status switch
        {
            ResultStatus.Invalid => AppResult<TTo>.Invalid(result.ValidationErrors),
            ResultStatus.NotFound => AppResult<TTo>.NotFound(result.Messages.FirstOrDefault() ?? "Resource was not found."),
            ResultStatus.Conflict => AppResult<TTo>.Conflict(result.Messages.FirstOrDefault() ?? "Resource conflicts with an existing resource."),
            ResultStatus.PayloadTooLarge => AppResult<TTo>.PayloadTooLarge(result.Messages.FirstOrDefault() ?? "Resource is too large."),
            _ => AppResult<TTo>.Error(result.Messages.FirstOrDefault() ?? "The operation failed.")
        };

    private static readonly Regex IndexNameRegex =
        new("^[a-z0-9][a-z0-9_-]{0,62}$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
}

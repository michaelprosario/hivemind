using System.Text.Json;
using FileRepo.Core.Common;
using FileRepo.Core.VectorSearch;
using FileRepo.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

return await Cli.RunAsync(args);

/// <summary>
/// Entry point logic for the File Repository vector database CLI. Supports
/// ingesting a single file or every supported file in a directory into a
/// named Qdrant vector index, reusing the same Core/Infrastructure services
/// as the HTTP API and MCP server.
/// </summary>
internal static class Cli
{
    public static async Task<int> RunAsync(string[] args)
    {
        if (args.Length == 0 || IsHelp(args[0]))
        {
            PrintUsage();
            return args.Length == 0 ? 1 : 0;
        }

        var command = args[0];
        var options = ParseOptions(args.AsSpan(1).ToArray());

        try
        {
            switch (command)
            {
                case "ingest-file":
                    return await RunIngestFileAsync(options);
                case "ingest-directory":
                    return await RunIngestDirectoryAsync(options);
                default:
                    Console.Error.WriteLine($"Unknown command '{command}'.");
                    PrintUsage();
                    return 1;
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }

    private static async Task<int> RunIngestFileAsync(CliOptions options)
    {
        if (!options.TryRequire("index", "path", out var missing))
        {
            Console.Error.WriteLine($"Missing required option --{missing}.");
            return 1;
        }

        using var host = BuildHost(options);
        using var scope = host.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IVectorDocumentService>();

        var result = await service.IngestAsync(
            new IngestVectorDocumentCommand(options.Index!, options.Path!),
            CancellationToken.None);

        return PrintResult(result);
    }

    private static async Task<int> RunIngestDirectoryAsync(CliOptions options)
    {
        if (!options.TryRequire("index", "path", out var missing))
        {
            Console.Error.WriteLine($"Missing required option --{missing}.");
            return 1;
        }

        using var host = BuildHost(options);
        using var scope = host.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IDirectoryIngestionService>();

        var result = await service.IngestDirectoryAsync(
            new IngestVectorDocumentDirectoryCommand(options.Index!, options.Path!, options.Recursive),
            CancellationToken.None);

        return PrintResult(result);
    }

    private static IHost BuildHost(CliOptions options)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration
            .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.json"), optional: true)
            .AddEnvironmentVariables()
            .AddCommandLine(options.RawArgs);

        // The CLI is a short-lived process; route logs to stderr so stdout
        // stays reserved for the JSON result payload.
        builder.Logging.ClearProviders();
        builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);

        builder.Services.AddVectorSearchInfrastructure(builder.Configuration);
        builder.Services.AddScoped<IVectorDocumentService, VectorDocumentService>();

        return builder.Build();
    }

    private static int PrintResult<T>(AppResult<T> result)
    {
        var json = JsonSerializer.Serialize(new
        {
            success = result.IsSuccess,
            status = result.Status.ToString(),
            data = result.Value,
            messages = result.Messages,
            validationErrors = result.ValidationErrors
        }, new JsonSerializerOptions { WriteIndented = true });

        Console.WriteLine(json);
        return result.IsSuccess ? 0 : 1;
    }

    private static bool IsHelp(string arg) => arg is "-h" or "--help" or "help";

    private static CliOptions ParseOptions(string[] rawArgs)
    {
        var options = new CliOptions { RawArgs = rawArgs };
        for (var i = 0; i < rawArgs.Length; i++)
        {
            var arg = rawArgs[i];
            string? value = null;
            if (i + 1 < rawArgs.Length && !rawArgs[i + 1].StartsWith('-'))
            {
                value = rawArgs[++i];
            }

            switch (arg)
            {
                case "--index" or "-i":
                    options.Index = value;
                    break;
                case "--path" or "-p":
                    options.Path = value;
                    break;
                case "--recursive" or "-r":
                    options.Recursive = true;
                    break;
            }
        }

        return options;
    }

    private static void PrintUsage()
    {
        Console.WriteLine(
            """
            File Repository vector database CLI

            Usage:
              filerepo-cli ingest-file --index <vectorIndexName> --path <filePath>
              filerepo-cli ingest-directory --index <vectorIndexName> --path <directoryPath> [--recursive]

            Options:
              -i, --index       Name of the Qdrant vector index to ingest into.
              -p, --path        Path to the file or directory to ingest.
              -r, --recursive   (ingest-directory only) Include subdirectories.
              -h, --help        Show this help message.

            Configuration (Qdrant endpoint, embedding model, allowed source root, etc.)
            is read from appsettings.json next to the CLI executable, and can be
            overridden with environment variables or extra command-line arguments
            (e.g. --VectorSearch:QdrantEndpoint http://localhost:6333).
            """);
    }
}

internal sealed class CliOptions
{
    public string[] RawArgs { get; init; } = [];
    public string? Index { get; set; }
    public string? Path { get; set; }
    public bool Recursive { get; set; }

    public bool TryRequire(string indexFieldName, string pathFieldName, out string missing)
    {
        if (string.IsNullOrWhiteSpace(Index))
        {
            missing = indexFieldName;
            return false;
        }

        if (string.IsNullOrWhiteSpace(Path))
        {
            missing = pathFieldName;
            return false;
        }

        missing = "";
        return true;
    }
}

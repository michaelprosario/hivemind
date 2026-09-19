using FileRepo.Core.VectorSearch;
using FileRepo.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.AspNetCore;

var configuration = new ConfigurationManager();
configuration
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: true)
    .AddEnvironmentVariables()
    .AddCommandLine(args);

var transport = configuration["Mcp:Transport"]?.Trim().ToLowerInvariant() ?? "stdio";
if (transport is not ("stdio" or "http"))
{
    throw new InvalidOperationException(
        $"Unsupported MCP transport '{transport}'. Set Mcp:Transport to 'stdio' or 'http'.");
}

if (transport == "http")
{
    var builder = WebApplication.CreateBuilder(args);
    ConfigureServices(builder.Services, builder.Configuration);

    builder.Services
        .AddMcpServer()
        .WithHttpTransport(options => options.SessionMode = HttpServerSessionMode.Stateless)
        .WithToolsFromAssembly();

    var app = builder.Build();
    app.MapMcp(builder.Configuration["Mcp:HttpPath"] ?? "/mcp");
    await app.RunAsync();
}
else
{
    var builder = Host.CreateApplicationBuilder(args);

    // Stdio uses stdout exclusively for MCP JSON-RPC messages, so every log
    // line must be routed to stderr instead.
    builder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);
    ConfigureServices(builder.Services, builder.Configuration);

    builder.Services
        .AddMcpServer()
        .WithStdioServerTransport()
        .WithToolsFromAssembly();

    await builder.Build().RunAsync();
}

static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
{
    services.AddVectorSearchInfrastructure(configuration);
    services.AddScoped<IVectorDocumentService, VectorDocumentService>();
}

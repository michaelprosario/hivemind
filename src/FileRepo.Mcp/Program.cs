using FileRepo.Core.VectorSearch;
using FileRepo.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);

// The stdio transport uses stdout exclusively for MCP JSON-RPC messages, so
// every log line must be routed to stderr instead.
builder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);

builder.Services.AddVectorSearchInfrastructure(builder.Configuration);
builder.Services.AddScoped<IVectorDocumentService, VectorDocumentService>();

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

await builder.Build().RunAsync();

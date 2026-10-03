using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;

namespace FileRepo.Mcp;

public sealed class McpApiKeyAuthenticationMiddleware
{
    private const string HeaderName = "X-API-Key";

    private readonly RequestDelegate next;
    private readonly byte[] expectedApiKey;
    private readonly PathString mcpPath;

    public McpApiKeyAuthenticationMiddleware(
        RequestDelegate next,
        string apiKey,
        PathString mcpPath)
    {
        this.next = next;
        expectedApiKey = Encoding.UTF8.GetBytes(apiKey);
        this.mcpPath = mcpPath;
    }

    public static string ValidateApiKey(string? apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException(
                "Mcp:ApiKey must be configured with a non-empty value when Mcp:Transport is http.");
        }

        return apiKey;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments(mcpPath, StringComparison.OrdinalIgnoreCase))
        {
            await next(context);
            return;
        }

        var headers = context.Request.Headers;
        if (!headers.TryGetValue(HeaderName, out var suppliedKeys)
            || suppliedKeys.Count != 1
            || string.IsNullOrEmpty(suppliedKeys[0])
            || suppliedKeys[0]!.Contains(',')
            || !ApiKeyMatches(suppliedKeys[0]!))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        await next(context);
    }

    private bool ApiKeyMatches(string suppliedApiKey)
    {
        var suppliedBytes = Encoding.UTF8.GetBytes(suppliedApiKey);
        return CryptographicOperations.FixedTimeEquals(expectedApiKey, suppliedBytes);
    }
}

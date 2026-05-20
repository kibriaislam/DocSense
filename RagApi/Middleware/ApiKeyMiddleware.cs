using System.Text.Json;
using Microsoft.Extensions.Options;
using RagApi.Infrastructure.Options;

namespace RagApi.Middleware;

// Endpoints that require authentication follow the [ApiKeyRequired] convention (comment only — not a real attribute).

public class ApiKeyMiddleware
{
    private const string ApiKeyHeaderName = "X-Api-Key";

    private readonly RequestDelegate _next;
    private readonly IOptions<ApiOptions> _apiOptions;

    public ApiKeyMiddleware(RequestDelegate next, IOptions<ApiOptions> apiOptions)
    {
        _next = next;
        _apiOptions = apiOptions;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (ShouldSkipAuthentication(context))
        {
            await _next(context);
            return;
        }

        if (!context.Request.Headers.TryGetValue(ApiKeyHeaderName, out var providedKey)
            || string.IsNullOrWhiteSpace(providedKey))
        {
            await WriteJsonErrorAsync(context, StatusCodes.Status401Unauthorized, "Missing API key");
            return;
        }

        if (!string.Equals(providedKey, _apiOptions.Value.Key, StringComparison.Ordinal))
        {
            await WriteJsonErrorAsync(context, StatusCodes.Status403Forbidden, "Invalid API key");
            return;
        }

        await _next(context);
    }

    private static bool ShouldSkipAuthentication(HttpContext context)
    {
        if (!HttpMethods.IsGet(context.Request.Method))
        {
            return false;
        }

        var path = context.Request.Path.Value ?? string.Empty;

        if (path.Equals("/health", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (path.StartsWith("/swagger", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (path.StartsWith("/swagger-ui", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    private static async Task WriteJsonErrorAsync(HttpContext context, int statusCode, string message)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";

        var payload = JsonSerializer.Serialize(new { error = message });
        await context.Response.WriteAsync(payload);
    }
}

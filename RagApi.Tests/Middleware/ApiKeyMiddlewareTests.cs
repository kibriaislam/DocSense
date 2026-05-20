using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using RagApi.Infrastructure.Options;
using RagApi.Middleware;

namespace RagApi.Tests.Middleware;

public class ApiKeyMiddlewareTests
{
    private const string ValidApiKey = "test-api-key";

    [Fact]
    public async Task Returns401_WhenHeaderMissing()
    {
        var context = CreateHttpContext();
        var nextCalled = false;

        var middleware = CreateMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
        Assert.False(nextCalled);
        Assert.Equal("Missing API key", await ReadErrorAsync(context));
    }

    [Fact]
    public async Task Returns403_WhenKeyWrong()
    {
        var context = CreateHttpContext();
        context.Request.Headers["X-Api-Key"] = "wrong-key";
        var nextCalled = false;

        var middleware = CreateMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
        Assert.False(nextCalled);
        Assert.Equal("Invalid API key", await ReadErrorAsync(context));
    }

    [Fact]
    public async Task CallsNext_WhenKeyCorrect()
    {
        var context = CreateHttpContext();
        context.Request.Headers["X-Api-Key"] = ValidApiKey;
        var nextCalled = false;

        var middleware = CreateMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        Assert.True(nextCalled);
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
    }

    private static ApiKeyMiddleware CreateMiddleware(RequestDelegate next) =>
        new(next, Options.Create(new ApiOptions { Key = ValidApiKey }));

    private static DefaultHttpContext CreateHttpContext()
    {
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Get;
        context.Request.Path = "/api/documents";
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static async Task<string> ReadErrorAsync(HttpContext context)
    {
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var document = await JsonDocument.ParseAsync(context.Response.Body);
        return document.RootElement.GetProperty("error").GetString()!;
    }
}

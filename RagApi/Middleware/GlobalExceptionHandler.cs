using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;

namespace RagApi.Middleware;

public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        _logger.LogError(exception, "Unhandled exception");

        var (statusCode, body) = exception switch
        {
            ValidationException validationException => (
                StatusCodes.Status400BadRequest,
                (object)new
                {
                    type = "validation_error",
                    errors = validationException.Errors.Select(error => error.ErrorMessage).ToArray()
                }),
            NotSupportedException notSupportedException => (
                StatusCodes.Status415UnsupportedMediaType,
                (object)new
                {
                    type = "unsupported_media_type",
                    message = notSupportedException.Message
                }),
            HttpRequestException => (
                StatusCodes.Status502BadGateway,
                (object)new
                {
                    type = "upstream_error",
                    message = "AI service unavailable. Is Ollama running?"
                }),
            _ => (
                StatusCodes.Status500InternalServerError,
                (object)new
                {
                    type = "internal_error",
                    message = "An unexpected error occurred"
                })
        };

        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response.WriteAsJsonAsync(body, cancellationToken);

        return true;
    }
}

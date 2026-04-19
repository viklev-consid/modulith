using System.Diagnostics;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Modulith.Api.Infrastructure.Exceptions;

internal sealed partial class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        LogUnhandledException(logger, exception.Message, exception);

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails =
            {
                Type = "https://tools.ietf.org/html/rfc7231#section-6.6.1",
                Title = "An unexpected error occurred.",
                Status = StatusCodes.Status500InternalServerError,
                Extensions =
                {
                    ["traceId"] = Activity.Current?.TraceId.ToString()
                }
            }
        });
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Unhandled exception: {Message}")]
    private static partial void LogUnhandledException(ILogger logger, string message, Exception exception);
}

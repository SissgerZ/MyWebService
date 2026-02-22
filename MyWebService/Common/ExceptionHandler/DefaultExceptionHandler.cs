using Microsoft.AspNetCore.Diagnostics;

namespace MyWebService.Common.ExceptionHandler;

public class DefaultExceptionHandler(ILogger<DefaultExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        logger.LogError(exception, "[{Handler}] Unhandled exception occurred: {ExceptionMessage}",
            nameof(DefaultExceptionHandler), exception.Message);

        Microsoft.AspNetCore.Mvc.ProblemDetails details = new()
        {
            Status = StatusCodes.Status500InternalServerError,
            Type = exception.GetType().Name,
            Title = "An unexpected error occurred while processing your request.",
#if DEBUG
            Detail = exception.Message,
#endif
            Instance = $"{httpContext.Request.Method} {httpContext.Request.Path}"
        };

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await httpContext.Response.WriteAsJsonAsync(details, cancellationToken);

        return true;
    }
}
using Api.Observability;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Api.Services;

public sealed class GlobalExceptionHandler(
    ILogger<GlobalExceptionHandler> logger,
    IWebHostEnvironment environment) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        AppTelemetry.ChatFailures.Add(1);
        logger.LogError(exception, "An unhandled exception occurred: {Message}", exception.Message);

        var detail = environment.IsDevelopment()
            ? exception.Message
            : "An unexpected error occurred. Please try again later.";

        var problemDetails = ApiErrorFactory.InternalServerError(
            detail,
            $"{httpContext.Request.Method} {httpContext.Request.Path}");

        httpContext.Response.StatusCode = problemDetails.Status ?? StatusCodes.Status500InternalServerError;

        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

        return true;
    }
}

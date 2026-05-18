using Api.Services;
using System.Diagnostics;

namespace Api.Middleware;

public sealed class EndpointSloTrackingMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, IEndpointSloTracker tracker)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            await next(context);
        }
        finally
        {
            stopwatch.Stop();
            var endpoint = context.GetEndpoint()?.DisplayName
                ?? context.Request.Path.Value
                ?? "unknown";
            tracker.Record(endpoint, context.Response.StatusCode, stopwatch.Elapsed.TotalMilliseconds);
        }
    }
}

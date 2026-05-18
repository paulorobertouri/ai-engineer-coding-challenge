using Api.Options;
using Microsoft.Extensions.Options;

namespace Api.Middleware;

public sealed class ChaosInjectionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ChaosOptions _options;
    private readonly Random _random;

    public ChaosInjectionMiddleware(RequestDelegate next, IOptions<ChaosOptions> options)
    {
        _next = next;
        _options = options.Value;
        _random = _options.Seed.HasValue ? new Random(_options.Seed.Value) : new Random();
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!_options.Enabled || !IsTargetPath(context.Request.Path))
        {
            await _next(context);
            return;
        }

        if (string.Equals(context.Request.Headers["X-Chaos-Bypass"], "true", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        if (_random.NextDouble() < _options.FailureRate)
        {
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            await context.Response.WriteAsJsonAsync(new
            {
                title = "Chaos profile injected failure.",
                status = StatusCodes.Status503ServiceUnavailable,
                detail = "Synthetic failure injected for resilience validation."
            });
            return;
        }

        await _next(context);
    }

    private bool IsTargetPath(PathString path)
    {
        var value = path.Value;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return _options.TargetPathPrefixes.Any(prefix =>
            value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }
}

using Api.Options;
using Api.Services;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace Api.Middleware;

public sealed class DistributedRateLimitingMiddleware(
    RequestDelegate next,
    IOptions<RateLimitingOptions> options,
    IDistributedRateLimitStore distributedRateLimitStore)
{
    private readonly RequestDelegate _next = next;
    private readonly RateLimitingOptions _options = options.Value;
    private readonly IDistributedRateLimitStore _distributedRateLimitStore = distributedRateLimitStore;

    public async Task InvokeAsync(HttpContext context)
    {
        if (!string.Equals(_options.Mode, RateLimitingMode.Distributed, StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        var policyName = ResolvePolicyName(context);
        if (string.IsNullOrWhiteSpace(policyName))
        {
            await _next(context);
            return;
        }

        var policy = ResolvePolicyOptions(policyName);
        if (policy is null)
        {
            await _next(context);
            return;
        }

        var partitionKey = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var acquired = await _distributedRateLimitStore.TryAcquireAsync(
            policyName,
            partitionKey,
            policy.PermitLimit,
            TimeSpan.FromSeconds(policy.WindowSeconds),
            context.RequestAborted);

        if (acquired)
        {
            await _next(context);
            return;
        }

        var problem = ApiErrorFactory.RateLimit(context.Request.Path);
        context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        await context.Response.WriteAsJsonAsync(problem, context.RequestAborted);
    }

    private static string? ResolvePolicyName(HttpContext context)
    {
        var endpoint = context.GetEndpoint();
        var metadata = endpoint?.Metadata.GetMetadata<EnableRateLimitingAttribute>();
        return metadata?.PolicyName;
    }

    private RateLimitingPolicyOptions? ResolvePolicyOptions(string policyName)
    {
        return policyName switch
        {
            "chat" => _options.Chat,
            "ingest" => _options.Ingest,
            _ => null
        };
    }
}

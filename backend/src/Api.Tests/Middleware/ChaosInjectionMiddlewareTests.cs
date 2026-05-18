using Api.Middleware;
using Api.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Xunit;

namespace Api.Tests.Middleware;

public sealed class ChaosInjectionMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_WhenEnabledAndFailureRateOne_ReturnsServiceUnavailable()
    {
        var middleware = new ChaosInjectionMiddleware(
            _ => Task.CompletedTask,
            Microsoft.Extensions.Options.Options.Create(new ChaosOptions
            {
                Enabled = true,
                FailureRate = 1.0,
                Seed = 42,
                TargetPathPrefixes = ["/api/v1/chat"]
            }));

        var context = new DefaultHttpContext();
        context.Request.Path = "/api/v1/chat";

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_WhenBypassHeaderSet_AllowsRequest()
    {
        var called = false;
        var middleware = new ChaosInjectionMiddleware(
            _ =>
            {
                called = true;
                return Task.CompletedTask;
            },
            Microsoft.Extensions.Options.Options.Create(new ChaosOptions
            {
                Enabled = true,
                FailureRate = 1.0,
                Seed = 42,
                TargetPathPrefixes = ["/api/v1/chat"]
            }));

        var context = new DefaultHttpContext();
        context.Request.Path = "/api/v1/chat";
        context.Request.Headers["X-Chaos-Bypass"] = "true";

        await middleware.InvokeAsync(context);

        Assert.True(called);
    }
}

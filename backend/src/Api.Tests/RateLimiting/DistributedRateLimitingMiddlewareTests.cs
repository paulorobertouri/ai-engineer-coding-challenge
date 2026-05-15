using Api.Middleware;
using Api.Options;
using Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Api.Tests.RateLimiting;

public class DistributedRateLimitingMiddlewareTests
{
    [Fact]
    public async Task InMemoryMode_BypassesDistributedStore()
    {
        var options = Microsoft.Extensions.Options.Options.Create(new RateLimitingOptions
        {
            Mode = RateLimitingMode.InMemory
        });

        var store = new Mock<IDistributedRateLimitStore>();
        var nextCalled = false;
        var middleware = new DistributedRateLimitingMiddleware(
            _ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            },
            options,
            store.Object);

        var context = BuildContextWithPolicy("ingest");
        await middleware.InvokeAsync(context);

        Assert.True(nextCalled);
        store.Verify(s => s.TryAcquireAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DistributedMode_WhenDenied_ReturnsTooManyRequests()
    {
        var options = Microsoft.Extensions.Options.Options.Create(new RateLimitingOptions
        {
            Mode = RateLimitingMode.Distributed,
            Ingest = new RateLimitingPolicyOptions { PermitLimit = 1, WindowSeconds = 60 }
        });

        var store = new Mock<IDistributedRateLimitStore>();
        store.Setup(s => s.TryAcquireAsync("ingest", It.IsAny<string>(), 1, TimeSpan.FromSeconds(60), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var middleware = new DistributedRateLimitingMiddleware(_ => Task.CompletedTask, options, store.Object);
        var context = BuildContextWithPolicy("ingest");

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status429TooManyRequests, context.Response.StatusCode);
    }

    [Fact]
    public async Task DistributedMode_WhenAllowed_CallsNext()
    {
        var options = Microsoft.Extensions.Options.Options.Create(new RateLimitingOptions
        {
            Mode = RateLimitingMode.Distributed,
            Ingest = new RateLimitingPolicyOptions { PermitLimit = 1, WindowSeconds = 60 }
        });

        var store = new Mock<IDistributedRateLimitStore>();
        store.Setup(s => s.TryAcquireAsync("ingest", It.IsAny<string>(), 1, TimeSpan.FromSeconds(60), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var nextCalled = false;
        var middleware = new DistributedRateLimitingMiddleware(
            _ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            },
            options,
            store.Object);

        var context = BuildContextWithPolicy("ingest");
        await middleware.InvokeAsync(context);

        Assert.True(nextCalled);
    }

    private static HttpContext BuildContextWithPolicy(string policyName)
    {
        var context = new DefaultHttpContext();
        var endpoint = new Endpoint(
            _ => Task.CompletedTask,
            new EndpointMetadataCollection(new EnableRateLimitingAttribute(policyName)),
            "rate-limited-endpoint");
        context.SetEndpoint(endpoint);
        return context;
    }
}

using Api.Services;
using Xunit;

namespace Api.Tests.RateLimiting;

public class InMemoryDistributedRateLimitStoreTests
{
    [Fact]
    public async Task TryAcquireAsync_RespectsPermitLimitWithinWindow()
    {
        var store = new InMemoryDistributedRateLimitStore();

        var first = await store.TryAcquireAsync("ingest", "127.0.0.1", permitLimit: 1, TimeSpan.FromMinutes(1));
        var second = await store.TryAcquireAsync("ingest", "127.0.0.1", permitLimit: 1, TimeSpan.FromMinutes(1));

        Assert.True(first);
        Assert.False(second);
    }

    [Fact]
    public async Task TryAcquireAsync_UsesSeparateCountersPerPartition()
    {
        var store = new InMemoryDistributedRateLimitStore();

        var first = await store.TryAcquireAsync("ingest", "127.0.0.1", permitLimit: 1, TimeSpan.FromMinutes(1));
        var secondPartition = await store.TryAcquireAsync("ingest", "127.0.0.2", permitLimit: 1, TimeSpan.FromMinutes(1));

        Assert.True(first);
        Assert.True(secondPartition);
    }

    [Fact]
    public async Task TryAcquireAsync_ConcurrentBurst_AllowsOnlyPermitLimit()
    {
        var store = new InMemoryDistributedRateLimitStore();
        var permitLimit = 5;

        var attempts = Enumerable.Range(0, 20)
            .Select(_ => store.TryAcquireAsync("chat", "127.0.0.1", permitLimit, TimeSpan.FromMinutes(1)).AsTask())
            .ToArray();

        var results = await Task.WhenAll(attempts);

        Assert.Equal(permitLimit, results.Count(allowed => allowed));
        Assert.Equal(20 - permitLimit, results.Count(allowed => !allowed));
    }
}

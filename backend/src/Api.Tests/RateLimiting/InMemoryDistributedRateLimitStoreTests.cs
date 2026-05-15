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
}

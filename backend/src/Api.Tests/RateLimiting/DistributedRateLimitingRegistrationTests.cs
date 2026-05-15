using Api.Options;
using Api.Services;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.RateLimiting;

public class DistributedRateLimitingRegistrationTests
{
    [Fact]
    public void InMemoryMode_DoesNotRequireDistributedProvider()
    {
        var services = new ServiceCollection();
        var options = new RateLimitingOptions
        {
            Mode = RateLimitingMode.InMemory,
            Distributed = new DistributedRateLimitingOptions { Provider = "redis" }
        };

        DistributedRateLimitingRegistration.ConfigureDistributedProvider(services, options);

        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(IDistributedRateLimitStore));
    }

    [Fact]
    public void DistributedRedisProvider_ThrowsClearError()
    {
        var services = new ServiceCollection();
        var options = new RateLimitingOptions
        {
            Mode = RateLimitingMode.Distributed,
            Distributed = new DistributedRateLimitingOptions { Provider = "redis" }
        };

        var error = Assert.Throws<InvalidOperationException>(() =>
            DistributedRateLimitingRegistration.ConfigureDistributedProvider(services, options));

        Assert.Contains("requires a Redis distributed rate limit adapter", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DistributedMemoryProvider_RegistersStore()
    {
        var services = new ServiceCollection();
        var options = new RateLimitingOptions
        {
            Mode = RateLimitingMode.Distributed,
            Distributed = new DistributedRateLimitingOptions { Provider = "memory" }
        };

        DistributedRateLimitingRegistration.ConfigureDistributedProvider(services, options);

        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IDistributedRateLimitStore));
    }
}

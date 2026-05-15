using Api.Options;

namespace Api.Services;

public static class DistributedRateLimitingRegistration
{
    public static bool IsDistributedEnabled(RateLimitingOptions options)
    {
        return string.Equals(options.Mode, RateLimitingMode.Distributed, StringComparison.OrdinalIgnoreCase);
    }

    public static void ConfigureDistributedProvider(IServiceCollection services, RateLimitingOptions options)
    {
        if (!IsDistributedEnabled(options))
        {
            return;
        }

        var provider = options.Distributed.Provider.Trim().ToLowerInvariant();
        switch (provider)
        {
            case "memory":
                services.AddSingleton<IDistributedRateLimitStore, InMemoryDistributedRateLimitStore>();
                break;
            case "redis":
                throw new InvalidOperationException(
                    "RateLimiting mode 'distributed' with provider 'redis' requires a Redis distributed rate limit adapter, which is not registered in this build.");
            default:
                throw new InvalidOperationException($"Unsupported distributed rate limiting provider '{options.Distributed.Provider}'.");
        }
    }
}

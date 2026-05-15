namespace Api.Services;

public interface IDistributedRateLimitStore
{
    ValueTask<bool> TryAcquireAsync(
        string policyName,
        string partitionKey,
        int permitLimit,
        TimeSpan window,
        CancellationToken cancellationToken = default);
}

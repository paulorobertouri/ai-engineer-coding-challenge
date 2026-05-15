using System.Collections.Concurrent;

namespace Api.Services;

public sealed class InMemoryDistributedRateLimitStore : IDistributedRateLimitStore
{
    private sealed class CounterState
    {
        public DateTimeOffset WindowStartUtc { get; set; }
        public int Count { get; set; }
    }

    private readonly ConcurrentDictionary<string, CounterState> _counters = new(StringComparer.Ordinal);

    public ValueTask<bool> TryAcquireAsync(
        string policyName,
        string partitionKey,
        int permitLimit,
        TimeSpan window,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var key = $"{policyName}:{partitionKey}";
        var now = DateTimeOffset.UtcNow;
        var state = _counters.GetOrAdd(key, _ => new CounterState { WindowStartUtc = now, Count = 0 });

        lock (state)
        {
            if (now - state.WindowStartUtc >= window)
            {
                state.WindowStartUtc = now;
                state.Count = 0;
            }

            if (state.Count >= permitLimit)
            {
                return ValueTask.FromResult(false);
            }

            state.Count++;
            return ValueTask.FromResult(true);
        }
    }
}

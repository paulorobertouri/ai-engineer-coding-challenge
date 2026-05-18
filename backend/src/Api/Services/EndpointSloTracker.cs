using System.Collections.Concurrent;
using Api.Contracts;

namespace Api.Services;

public interface IEndpointSloTracker
{
    void Record(string endpoint, int statusCode, double latencyMs);

    EndpointSloSummaryResponse BuildSummary(int maxEndpoints = 20);
}

public sealed class InMemoryEndpointSloTracker : IEndpointSloTracker
{
    private const int MaxLatencySamplesPerEndpoint = 500;
    private readonly ConcurrentDictionary<string, EndpointSloBucket> _buckets = new(StringComparer.OrdinalIgnoreCase);

    public void Record(string endpoint, int statusCode, double latencyMs)
    {
        var normalizedEndpoint = string.IsNullOrWhiteSpace(endpoint) ? "unknown" : endpoint.Trim();
        var bucket = _buckets.GetOrAdd(normalizedEndpoint, _ => new EndpointSloBucket());
        bucket.Record(statusCode, latencyMs);
    }

    public EndpointSloSummaryResponse BuildSummary(int maxEndpoints = 20)
    {
        var normalizedMax = Math.Clamp(maxEndpoints, 1, 100);

        var reports = _buckets
            .Select(entry => BuildReport(entry.Key, entry.Value))
            .OrderByDescending(report => report.RequestCount)
            .Take(normalizedMax)
            .ToList();

        return new EndpointSloSummaryResponse
        {
            GeneratedAtUtc = DateTimeOffset.UtcNow,
            Endpoints = reports
        };
    }

    private static EndpointSloReportDto BuildReport(string endpoint, EndpointSloBucket bucket)
    {
        var snapshot = bucket.Snapshot();
        var requestCount = snapshot.RequestCount;
        var errorCount = snapshot.ErrorCount;

        var averageLatency = requestCount == 0 ? 0 : snapshot.TotalLatencyMs / requestCount;
        var p95Latency = CalculatePercentile(snapshot.Latencies, 0.95);

        return new EndpointSloReportDto
        {
            Endpoint = endpoint,
            RequestCount = requestCount,
            ErrorCount = errorCount,
            ErrorRate = requestCount == 0 ? 0 : (double)errorCount / requestCount,
            AverageLatencyMs = averageLatency,
            P95LatencyMs = p95Latency
        };
    }

    private static double CalculatePercentile(IReadOnlyList<double> values, double percentile)
    {
        if (values.Count == 0)
        {
            return 0;
        }

        var ordered = values.OrderBy(value => value).ToArray();
        var index = (int)Math.Ceiling(percentile * ordered.Length) - 1;
        var boundedIndex = Math.Clamp(index, 0, ordered.Length - 1);
        return ordered[boundedIndex];
    }

    private sealed class EndpointSloBucket
    {
        private readonly object _sync = new();
        private readonly Queue<double> _latencySamples = new();
        private int _requestCount;
        private int _errorCount;
        private double _totalLatencyMs;

        public void Record(int statusCode, double latencyMs)
        {
            lock (_sync)
            {
                _requestCount++;
                if (statusCode >= 500)
                {
                    _errorCount++;
                }

                _totalLatencyMs += latencyMs;
                _latencySamples.Enqueue(Math.Max(0, latencyMs));
                while (_latencySamples.Count > MaxLatencySamplesPerEndpoint)
                {
                    _latencySamples.Dequeue();
                }
            }
        }

        public EndpointSloSnapshot Snapshot()
        {
            lock (_sync)
            {
                return new EndpointSloSnapshot(
                    _requestCount,
                    _errorCount,
                    _totalLatencyMs,
                    _latencySamples.ToArray());
            }
        }
    }

    private sealed record EndpointSloSnapshot(
        int RequestCount,
        int ErrorCount,
        double TotalLatencyMs,
        IReadOnlyList<double> Latencies);
}

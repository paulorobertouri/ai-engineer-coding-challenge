using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Api.Observability;

public static class AppTelemetry
{
    public const string ServiceName = "grocery-store-sop-assistant-api";
    public const string ActivitySourceName = "GroceryStore.SopAssistant";
    public const string MeterName = "GroceryStore.SopAssistant.Metrics";

    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);
    public static readonly Meter Meter = new(MeterName);

    public static readonly Counter<long> ChatRequests = Meter.CreateCounter<long>(
        "chat.requests",
        unit: "requests",
        description: "Total chat requests processed.");

    public static readonly Histogram<double> ChatLatencyMs = Meter.CreateHistogram<double>(
        "chat.latency.ms",
        unit: "ms",
        description: "Chat response latency in milliseconds.");

    public static readonly Counter<long> ChatFailures = Meter.CreateCounter<long>(
        "chat.failures",
        unit: "failures",
        description: "Total chat failures.");

    public static readonly Histogram<double> VectorSearchLatencyMs = Meter.CreateHistogram<double>(
        "vector.search.latency.ms",
        unit: "ms",
        description: "Vector search latency in milliseconds.");

    public static readonly Histogram<double> OpenAiCallLatencyMs = Meter.CreateHistogram<double>(
        "openai.call.latency.ms",
        unit: "ms",
        description: "OpenAI chat completion call latency in milliseconds.");

    public static readonly Counter<long> IngestChunks = Meter.CreateCounter<long>(
        "ingest.chunks",
        unit: "chunks",
        description: "Total chunks produced during ingestion.");

    public static readonly Histogram<double> IngestLatencyMs = Meter.CreateHistogram<double>(
        "ingest.latency.ms",
        unit: "ms",
        description: "Ingestion processing latency in milliseconds.");

    public static readonly Counter<long> IngestFailures = Meter.CreateCounter<long>(
        "ingest.failures",
        unit: "failures",
        description: "Total ingest failures.");

    public static readonly Counter<long> RateLimitRejections = Meter.CreateCounter<long>(
        "http.rate_limit.rejections",
        unit: "rejections",
        description: "Total HTTP requests rejected by rate limiting.");
}

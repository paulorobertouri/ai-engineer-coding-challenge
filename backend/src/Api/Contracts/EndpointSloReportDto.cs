namespace Api.Contracts;

public sealed class EndpointSloReportDto
{
    public string Endpoint { get; init; } = string.Empty;

    public int RequestCount { get; init; }

    public int ErrorCount { get; init; }

    public double ErrorRate { get; init; }

    public double P95LatencyMs { get; init; }

    public double AverageLatencyMs { get; init; }
}
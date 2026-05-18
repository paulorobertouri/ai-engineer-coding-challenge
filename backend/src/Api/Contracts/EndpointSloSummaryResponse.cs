namespace Api.Contracts;

public sealed class EndpointSloSummaryResponse
{
    public DateTimeOffset GeneratedAtUtc { get; init; }

    public IReadOnlyList<EndpointSloReportDto> Endpoints { get; init; } = [];
}
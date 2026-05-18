namespace Api.Contracts;

public sealed class RetrievalBenchmarkDashboardResponse
{
    public DateTimeOffset GeneratedAtUtc { get; init; }

    public IReadOnlyList<RetrievalBenchmarkEntryDto> Entries { get; init; } = [];
}

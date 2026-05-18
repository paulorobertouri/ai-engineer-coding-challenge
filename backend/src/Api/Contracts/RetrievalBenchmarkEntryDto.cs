namespace Api.Contracts;

public sealed class RetrievalBenchmarkEntryDto
{
    public Guid RunId { get; init; }

    public DateTimeOffset TimestampUtc { get; init; }

    public string Commit { get; init; } = string.Empty;

    public int FixtureCount { get; init; }

    public double Precision { get; init; }

    public double Recall { get; init; }
}

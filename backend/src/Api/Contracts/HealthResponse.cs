namespace Api.Contracts;

public sealed class HealthResponse
{
    public string Status { get; init; } = string.Empty;

    public string Service { get; init; } = string.Empty;

    public DateTimeOffset UtcTime { get; init; }

    public List<string> Notes { get; init; } = [];

    public bool IsIngested { get; init; }

    public int RecordCount { get; init; }
}
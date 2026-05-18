namespace Api.Contracts;

public sealed class ReadinessHistoryEntryDto
{
    public string? PreviousStatus { get; init; }

    public string CurrentStatus { get; init; } = string.Empty;

    public DateTimeOffset ChangedAtUtc { get; init; }

    public List<string> Notes { get; init; } = [];
}
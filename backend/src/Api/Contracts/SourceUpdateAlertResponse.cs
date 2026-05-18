namespace Api.Contracts;

public sealed class SourceUpdateAlertResponse
{
    public string KnowledgeBaseId { get; init; } = string.Empty;

    public bool RequiresReingestReview { get; init; }

    public string? CurrentSourceChecksum { get; init; }

    public string? IngestedSourceChecksum { get; init; }

    public DateTimeOffset DetectedAtUtc { get; init; }

    public string Message { get; init; } = string.Empty;
}

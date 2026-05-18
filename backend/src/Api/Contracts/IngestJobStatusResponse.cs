namespace Api.Contracts;

public sealed class IngestJobStatusResponse
{
    public Guid JobId { get; init; }

    public string KnowledgeBaseId { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;

    public DateTimeOffset QueuedAtUtc { get; init; }

    public DateTimeOffset? StartedAtUtc { get; init; }

    public DateTimeOffset? CompletedAtUtc { get; init; }

    public IngestResponse? Result { get; init; }

    public string? ErrorMessage { get; init; }

    public int Priority { get; init; }
}

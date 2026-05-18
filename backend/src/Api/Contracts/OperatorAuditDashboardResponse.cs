namespace Api.Contracts;

public sealed class OperatorAuditDashboardResponse
{
    public DateTimeOffset GeneratedAtUtc { get; init; }

    public DateTimeOffset FromUtc { get; init; }

    public DateTimeOffset ToUtc { get; init; }

    public string? KnowledgeBaseId { get; init; }

    public string? FeedbackTypeFilter { get; init; }

    public int FeedbackCount { get; init; }

    public int LowConfidenceSignalCount { get; init; }

    public int FailedIngestCount { get; init; }

    public IReadOnlyList<OperatorAuditEntryDto> Feedback { get; init; } = [];

    public IReadOnlyList<OperatorAuditEntryDto> LowConfidenceSignals { get; init; } = [];

    public IReadOnlyList<OperatorAuditEntryDto> FailedIngests { get; init; } = [];
}
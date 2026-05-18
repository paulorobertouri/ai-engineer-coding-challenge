namespace Api.Contracts;

public sealed class OperatorAuditEntryDto
{
    public DateTimeOffset TimestampUtc { get; init; }

    public string Type { get; init; } = string.Empty;

    public string Severity { get; init; } = "info";

    public string ConversationId { get; init; } = string.Empty;

    public string MessageId { get; init; } = string.Empty;

    public string FeedbackType { get; init; } = string.Empty;

    public string? Comment { get; init; }

    public string Action { get; init; } = string.Empty;

    public string Outcome { get; init; } = string.Empty;

    public string KnowledgeBaseId { get; init; } = string.Empty;

    public string SourceName { get; init; } = string.Empty;

    public string? SafeSummary { get; init; }
}
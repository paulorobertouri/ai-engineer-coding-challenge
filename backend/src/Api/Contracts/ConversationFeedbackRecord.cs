namespace Api.Contracts;

public sealed record ConversationFeedbackRecord
{
    public DateTimeOffset TimestampUtc { get; init; }

    public string ConversationId { get; init; } = string.Empty;

    public string MessageId { get; init; } = string.Empty;

    public string FeedbackType { get; init; } = string.Empty;

    public string? Comment { get; init; }
}

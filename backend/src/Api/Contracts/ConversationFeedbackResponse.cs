namespace Api.Contracts;

public sealed class ConversationFeedbackResponse
{
    public bool Accepted { get; init; }

    public string Message { get; init; } = string.Empty;

    public DateTimeOffset SubmittedAtUtc { get; init; }
}

namespace Api.Application.Feedback;

public sealed record SubmitConversationFeedbackCommand(
    string ConversationId,
    string MessageId,
    string FeedbackType,
    string? Comment);

using Api.Contracts;
using Api.Services;

namespace Api.Application.Feedback;

public sealed class SubmitConversationFeedbackHandler(IConversationFeedbackService feedbackService)
{
    public async Task<ConversationFeedbackResponse> HandleAsync(
        SubmitConversationFeedbackCommand command,
        CancellationToken cancellationToken)
    {
        var normalizedComment = string.IsNullOrWhiteSpace(command.Comment)
            ? null
            : command.Comment.Trim();

        await feedbackService.RecordAsync(new ConversationFeedbackRecord
        {
            TimestampUtc = DateTimeOffset.UtcNow,
            ConversationId = command.ConversationId,
            MessageId = command.MessageId,
            FeedbackType = command.FeedbackType,
            Comment = normalizedComment
        }, cancellationToken);

        return new ConversationFeedbackResponse
        {
            Accepted = true,
            Message = "Feedback submitted successfully.",
            SubmittedAtUtc = DateTimeOffset.UtcNow
        };
    }
}

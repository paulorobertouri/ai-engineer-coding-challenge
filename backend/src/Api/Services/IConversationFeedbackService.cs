using Api.Contracts;

namespace Api.Services;

public interface IConversationFeedbackService
{
    Task RecordAsync(ConversationFeedbackRecord record, CancellationToken cancellationToken = default);
}

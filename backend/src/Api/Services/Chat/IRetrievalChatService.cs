using Api.Contracts;

namespace Api.Services;

public interface IRetrievalChatService
{
    Task<ChatResponse> GenerateResponseAsync(ChatRequest request, CancellationToken cancellationToken = default);
}
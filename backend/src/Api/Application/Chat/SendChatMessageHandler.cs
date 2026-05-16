using Api.Contracts;
using Api.Options;
using Api.Services;
using Microsoft.Extensions.Options;

namespace Api.Application.Chat;

public sealed class SendChatMessageHandler(
    IRetrievalChatService retrievalChatService,
    IOptions<TimeoutOptions> timeoutOptions)
{
    public async Task<ChatResponse> HandleAsync(SendChatMessageCommand command, CancellationToken cancellationToken)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(timeoutOptions.Value.ChatSeconds));

        return await retrievalChatService.GenerateResponseAsync(command.Request, timeoutCts.Token);
    }
}

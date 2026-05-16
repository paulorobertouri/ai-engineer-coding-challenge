using Api.Application.Chat;
using Api.Contracts;
using Api.Options;
using Api.Services;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Api.Tests.Application;

public sealed class SendChatMessageHandlerTests
{
    [Fact]
    public async Task HandleAsync_ForwardsRequestToRetrievalService()
    {
        var mockService = new Mock<IRetrievalChatService>();
        var expected = new ChatResponse
        {
            Status = "success",
            AssistantMessage = "hello",
            ToolCalls = [],
            Citations = []
        };

        mockService
            .Setup(s => s.GenerateResponseAsync(It.IsAny<ChatRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var handler = new SendChatMessageHandler(
            mockService.Object,
            Microsoft.Extensions.Options.Options.Create(new TimeoutOptions { ChatSeconds = 30 }));

        var command = new SendChatMessageCommand(new ChatRequest
        {
            Messages = [new ChatMessageDto { Role = "user", Content = "Hi" }]
        });

        var response = await handler.HandleAsync(command, CancellationToken.None);

        Assert.Equal("hello", response.AssistantMessage);
        mockService.Verify(
            s => s.GenerateResponseAsync(
                It.Is<ChatRequest>(r => r.Messages.Count == 1 && r.Messages[0].Content == "Hi"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}

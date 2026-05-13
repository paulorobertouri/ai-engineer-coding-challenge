using Api.Contracts;
using Api.Controllers;
using Api.Services;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace Api.Tests;

public class ChatControllerTests
{
    private readonly Mock<IRetrievalChatService> _mockService = new();
    private readonly ChatController _controller;

    public ChatControllerTests()
    {
        _controller = new ChatController(_mockService.Object);
    }

    [Fact]
    public async Task Post_EmptyMessages_ReturnsBadRequest()
    {
        var request = new ChatRequest { Messages = [] };
        var result = await _controller.Post(request, CancellationToken.None);
        Assert.IsType<BadRequestObjectResult>(result.Result);
        _mockService.Verify(
            s => s.GenerateResponseAsync(It.IsAny<ChatRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Post_TooManyMessages_ReturnsValidationProblem()
    {
        var request = new ChatRequest
        {
            Messages = Enumerable
                .Range(1, ChatRequest.MaxMessages + 1)
                .Select(_ => new ChatMessageDto { Role = "user", Content = "hello" })
                .ToList()
        };

        var result = await _controller.Post(request, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
        _mockService.Verify(
            s => s.GenerateResponseAsync(It.IsAny<ChatRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Post_TooLongConversationId_ReturnsValidationProblem()
    {
        var request = new ChatRequest
        {
            ConversationId = new string('a', ChatRequest.MaxConversationIdLength + 1),
            Messages = [new ChatMessageDto { Role = "user", Content = "Hi" }]
        };

        var result = await _controller.Post(request, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
        _mockService.Verify(
            s => s.GenerateResponseAsync(It.IsAny<ChatRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Post_TooLongMessageContent_ReturnsValidationProblem()
    {
        var request = new ChatRequest
        {
            Messages =
            [
                new ChatMessageDto
                {
                    Role = "user",
                    Content = new string('x', ChatRequest.MaxMessageContentLength + 1)
                }
            ]
        };

        var result = await _controller.Post(request, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
        _mockService.Verify(
            s => s.GenerateResponseAsync(It.IsAny<ChatRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Post_NoUserMessage_ReturnsValidationProblem()
    {
        var request = new ChatRequest
        {
            Messages = [new ChatMessageDto { Role = "assistant", Content = "Hello" }]
        };

        var result = await _controller.Post(request, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
        _mockService.Verify(
            s => s.GenerateResponseAsync(It.IsAny<ChatRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Post_ValidRequest_ReturnsOkWithChatResponse()
    {
        var chatResponse = new ChatResponse
        {
            ConversationId = "conv-1",
            AssistantMessage = "Hello",
            Status = "success",
            ToolCalls = [],
            Citations = []
        };

        _mockService
            .Setup(s => s.GenerateResponseAsync(It.IsAny<ChatRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(chatResponse);

        var request = new ChatRequest
        {
            Messages = [new ChatMessageDto { Role = "user", Content = "Hi" }]
        };

        var result = await _controller.Post(request, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ChatResponse>(ok.Value);
        Assert.Equal("Hello", response.AssistantMessage);
    }

    [Fact]
    public async Task Post_PassesRequestToService()
    {
        var capturedRequest = default(ChatRequest);
        _mockService
            .Setup(s => s.GenerateResponseAsync(It.IsAny<ChatRequest>(), It.IsAny<CancellationToken>()))
            .Callback<ChatRequest, CancellationToken>((r, _) => capturedRequest = r)
            .ReturnsAsync(new ChatResponse { Status = "success", ToolCalls = [], Citations = [] });

        var request = new ChatRequest
        {
            ConversationId = "test-id",
            Messages = [new ChatMessageDto { Role = "user", Content = "Query" }]
        };

        await _controller.Post(request, CancellationToken.None);

        Assert.NotNull(capturedRequest);
        Assert.Equal("test-id", capturedRequest!.ConversationId);
    }
}

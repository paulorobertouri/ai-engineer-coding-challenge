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

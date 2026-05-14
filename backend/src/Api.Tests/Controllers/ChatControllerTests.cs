using Api.Contracts;
using Api.Options;
using Api.Controllers;
using Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Api.Tests;

public class ChatControllerTests
{
    private readonly Mock<IRetrievalChatService> _mockService = new();
    private readonly ChatController _controller;

    public ChatControllerTests()
    {
        _controller = new ChatController(
            _mockService.Object,
            Microsoft.Extensions.Options.Options.Create(new TimeoutOptions { ChatSeconds = 30 }));
    }

    [Fact]
    public async Task Post_EmptyMessages_ReturnsBadRequest()
    {
        var request = new ChatRequest { Messages = [] };
        var result = await _controller.Post(request, CancellationToken.None);
        var bad = Assert.IsType<BadRequestObjectResult>(result.Result);
        var details = Assert.IsType<ValidationProblemDetails>(bad.Value);
        Assert.Equal(ApiErrorFactory.ValidationErrorCode, details.Extensions["code"]);
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

    [Fact]
    public async Task Post_WhenServiceCancels_ReturnsRequestTimeoutProblemDetails()
    {
        _mockService
            .Setup(s => s.GenerateResponseAsync(It.IsAny<ChatRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var request = new ChatRequest
        {
            Messages = [new ChatMessageDto { Role = "user", Content = "Hi" }]
        };

        var result = await _controller.Post(request, CancellationToken.None);

        var timeout = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status408RequestTimeout, timeout.StatusCode);
        var details = Assert.IsType<ProblemDetails>(timeout.Value);
        Assert.Equal(ApiErrorFactory.RequestTimeoutErrorCode, details.Extensions["code"]);
    }

    [Fact]
    public async Task Post_WhenRequestIsCancelled_ThrowsOperationCanceledException()
    {
        _mockService
            .Setup(s => s.GenerateResponseAsync(It.IsAny<ChatRequest>(), It.IsAny<CancellationToken>()))
            .Returns(async (ChatRequest _, CancellationToken token) =>
            {
                await Task.Delay(10, token);
                return new ChatResponse();
            });

        var request = new ChatRequest
        {
            Messages = [new ChatMessageDto { Role = "user", Content = "Hi" }]
        };

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _controller.Post(request, cts.Token));
    }
}

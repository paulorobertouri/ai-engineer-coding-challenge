using Api.Application.Feedback;
using Api.Contracts;
using Api.Services;
using Moq;
using Xunit;

namespace Api.Tests.Application;

public sealed class SubmitConversationFeedbackHandlerTests
{
    [Fact]
    public async Task HandleAsync_TrimsCommentAndPersistsRecord()
    {
        var mockService = new Mock<IConversationFeedbackService>();
        ConversationFeedbackRecord? captured = null;

        mockService
            .Setup(s => s.RecordAsync(It.IsAny<ConversationFeedbackRecord>(), It.IsAny<CancellationToken>()))
            .Callback<ConversationFeedbackRecord, CancellationToken>((record, _) => captured = record)
            .Returns(Task.CompletedTask);

        var handler = new SubmitConversationFeedbackHandler(mockService.Object);

        var response = await handler.HandleAsync(
            new SubmitConversationFeedbackCommand("conv-1", "msg-1", "not_helpful", "  needs more detail  "),
            CancellationToken.None);

        Assert.True(response.Accepted);
        Assert.Equal("needs more detail", captured?.Comment);
        Assert.Equal("conv-1", captured?.ConversationId);
    }
}

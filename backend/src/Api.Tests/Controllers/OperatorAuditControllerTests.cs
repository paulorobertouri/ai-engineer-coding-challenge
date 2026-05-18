using Api.Contracts;
using Api.Application.Operators;
using Api.Services;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace Api.Tests;

public sealed class OperatorAuditControllerTests
{
    [Fact]
    public async Task GetDashboard_AggregatesFeedbackAndFailedIngests()
    {
        var feedbackService = new Mock<IConversationFeedbackService>();
        feedbackService
            .Setup(service => service.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new ConversationFeedbackRecord
                {
                    TimestampUtc = DateTimeOffset.UtcNow.AddHours(-1),
                    ConversationId = "conv-1",
                    MessageId = "msg-1",
                    FeedbackType = "wrong-citation",
                    Comment = "Not supported by evidence."
                },
                new ConversationFeedbackRecord
                {
                    TimestampUtc = DateTimeOffset.UtcNow.AddHours(-2),
                    ConversationId = "conv-2",
                    MessageId = "msg-2",
                    FeedbackType = "helpful"
                }
            ]);

        var ingestionAuditService = new Mock<IIngestionAuditService>();
        ingestionAuditService
            .Setup(service => service.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new IngestionAuditRecord
                {
                    TimestampUtc = DateTimeOffset.UtcNow.AddHours(-1),
                    Outcome = "failure",
                    Action = "upload-ingest",
                    KnowledgeBaseId = "default",
                    SourceName = "SOP.md",
                    SafeSummary = "Parse failure"
                },
                new IngestionAuditRecord
                {
                    TimestampUtc = DateTimeOffset.UtcNow.AddHours(-1),
                    Outcome = "success",
                    Action = "default-ingest",
                    KnowledgeBaseId = "default",
                    SourceName = "SOP.md"
                }
            ]);

        var handler = new OperatorAuditEndpointsHandler(feedbackService.Object, ingestionAuditService.Object);

        var result = await handler.GetDashboard("default", null, 24, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<OperatorAuditDashboardResponse>(ok.Value);
        Assert.Equal(2, payload.FeedbackCount);
        Assert.Equal(1, payload.LowConfidenceSignalCount);
        Assert.Equal(1, payload.FailedIngestCount);
        Assert.Single(payload.FailedIngests);
        Assert.Equal("default", payload.KnowledgeBaseId);
    }

    [Fact]
    public async Task GetDashboard_AppliesFeedbackFilter()
    {
        var feedbackService = new Mock<IConversationFeedbackService>();
        feedbackService
            .Setup(service => service.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new ConversationFeedbackRecord
                {
                    TimestampUtc = DateTimeOffset.UtcNow.AddHours(-1),
                    ConversationId = "conv-1",
                    MessageId = "msg-1",
                    FeedbackType = "wrong-citation"
                },
                new ConversationFeedbackRecord
                {
                    TimestampUtc = DateTimeOffset.UtcNow.AddHours(-1),
                    ConversationId = "conv-2",
                    MessageId = "msg-2",
                    FeedbackType = "helpful"
                }
            ]);

        var ingestionAuditService = new Mock<IIngestionAuditService>();
        ingestionAuditService
            .Setup(service => service.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var handler = new OperatorAuditEndpointsHandler(feedbackService.Object, ingestionAuditService.Object);

        var result = await handler.GetDashboard(null, "helpful", 24, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<OperatorAuditDashboardResponse>(ok.Value);
        Assert.Equal(1, payload.FeedbackCount);
        Assert.Equal("helpful", payload.FeedbackTypeFilter);
        Assert.Equal("helpful", payload.Feedback[0].FeedbackType);
    }
}

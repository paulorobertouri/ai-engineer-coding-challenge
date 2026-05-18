using Api.Contracts;
using Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Api.Application.Operators;

public sealed class OperatorAuditEndpointsHandler(
    IConversationFeedbackService conversationFeedbackService,
    IIngestionAuditService ingestionAuditService)
{
    public async Task<ActionResult<OperatorAuditDashboardResponse>> GetDashboard(
        string? knowledgeBaseId,
        string? feedbackType,
        int lookbackHours = 168,
        CancellationToken cancellationToken = default)
    {
        var normalizedLookbackHours = Math.Clamp(lookbackHours, 1, 24 * 30);
        var toUtc = DateTimeOffset.UtcNow;
        var fromUtc = toUtc.AddHours(-normalizedLookbackHours);
        var normalizedKnowledgeBaseId = string.IsNullOrWhiteSpace(knowledgeBaseId)
            ? null
            : knowledgeBaseId.Trim();
        var normalizedFeedbackType = string.IsNullOrWhiteSpace(feedbackType)
            ? null
            : feedbackType.Trim().ToLowerInvariant();

        var feedback = (await conversationFeedbackService.ListAsync(cancellationToken))
            .Where(entry => entry.TimestampUtc >= fromUtc && entry.TimestampUtc <= toUtc)
            .Where(entry => normalizedFeedbackType is null
                || string.Equals(entry.FeedbackType, normalizedFeedbackType, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(entry => entry.TimestampUtc)
            .Take(100)
            .ToList();

        var failedIngests = (await ingestionAuditService.ListAsync(cancellationToken))
            .Where(entry => entry.TimestampUtc >= fromUtc && entry.TimestampUtc <= toUtc)
            .Where(entry => string.Equals(entry.Outcome, "failure", StringComparison.OrdinalIgnoreCase))
            .Where(entry => normalizedKnowledgeBaseId is null
                || string.Equals(entry.KnowledgeBaseId, normalizedKnowledgeBaseId, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(entry => entry.TimestampUtc)
            .Take(100)
            .ToList();

        var lowConfidenceSignals = feedback
            .Where(entry => string.Equals(entry.FeedbackType, "unhelpful", StringComparison.OrdinalIgnoreCase)
                || string.Equals(entry.FeedbackType, "wrong-citation", StringComparison.OrdinalIgnoreCase))
            .ToList();

        return new OkObjectResult(new OperatorAuditDashboardResponse
        {
            GeneratedAtUtc = toUtc,
            FromUtc = fromUtc,
            ToUtc = toUtc,
            KnowledgeBaseId = normalizedKnowledgeBaseId,
            FeedbackTypeFilter = normalizedFeedbackType,
            FeedbackCount = feedback.Count,
            LowConfidenceSignalCount = lowConfidenceSignals.Count,
            FailedIngestCount = failedIngests.Count,
            Feedback = feedback.Select(entry => new OperatorAuditEntryDto
            {
                TimestampUtc = entry.TimestampUtc,
                Type = "feedback",
                Severity = string.Equals(entry.FeedbackType, "helpful", StringComparison.OrdinalIgnoreCase) ? "info" : "warning",
                ConversationId = entry.ConversationId,
                MessageId = entry.MessageId,
                FeedbackType = entry.FeedbackType,
                Comment = entry.Comment
            }).ToList(),
            LowConfidenceSignals = lowConfidenceSignals.Select(entry => new OperatorAuditEntryDto
            {
                TimestampUtc = entry.TimestampUtc,
                Type = "low_confidence",
                Severity = "warning",
                ConversationId = entry.ConversationId,
                MessageId = entry.MessageId,
                FeedbackType = entry.FeedbackType,
                Comment = entry.Comment
            }).ToList(),
            FailedIngests = failedIngests.Select(entry => new OperatorAuditEntryDto
            {
                TimestampUtc = entry.TimestampUtc,
                Type = "failed_ingest",
                Severity = "error",
                Action = entry.Action,
                Outcome = entry.Outcome,
                KnowledgeBaseId = entry.KnowledgeBaseId,
                SourceName = entry.SourceName,
                SafeSummary = entry.SafeSummary
            }).ToList()
        });
    }
}
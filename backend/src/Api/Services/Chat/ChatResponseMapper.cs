using Api.Contracts;
using Api.Models;

namespace Api.Services;

public static class ChatResponseMapper
{
    private const string FaithfulnessFailureReason = "citation_faithfulness_failed";
    private const string FaithfulnessFailureMessage =
        "I could not fully verify that answer against the cited SOP evidence, so I am returning a grounded fallback. Please refine your question for a more precise cited response.";

    public static ChatResponse FromMatches(
        string conversationId,
        string status,
        bool isPlaceholder,
        string assistantMessage,
        IReadOnlyList<VectorSearchMatch> matches,
        ChatUsageDto usage,
        string? userRole = null,
        string? responseLanguage = null,
        string? notFoundReason = null)
    {
        var citedChunkIds = matches.Select(m => m.Record.Id).ToList();
        if (CitationFaithfulnessGuard.HasUnsupportedClaims(assistantMessage, matches))
        {
            return NoContext(
                conversationId: conversationId,
                status: status,
                isPlaceholder: false,
                assistantMessage: FaithfulnessFailureMessage,
                reason: FaithfulnessFailureReason,
                usage: usage,
                userRole: userRole,
                responseLanguage: responseLanguage);
        }

        var followUpSuggestions = FollowUpSuggestionFactory.Create(matches, userRole, responseLanguage, notFoundReason);

        return new ChatResponse
        {
            ConversationId = conversationId,
            Status = status,
            IsPlaceholder = isPlaceholder,
            AssistantMessage = assistantMessage,
            ToolCalls = [],
            Citations = matches.Select(CitationMapper.FromMatch).ToList(),
            StructuredOutput = StructuredAnswerFactory.Create(
                assistantMessage,
                citedChunkIds,
                notFoundReason,
                followUpSuggestions),
            Confidence = ConfidenceIndicatorFactory.Create(matches, citedChunkIds),
            Usage = usage
        };
    }

    public static ChatResponse NoContext(
        string conversationId,
        string status,
        bool isPlaceholder,
        string assistantMessage,
        string reason,
        ChatUsageDto usage,
        string? userRole = null,
        string? responseLanguage = null)
    {
        var followUpSuggestions = FollowUpSuggestionFactory.Create([], userRole, responseLanguage, reason);

        return new ChatResponse
        {
            ConversationId = conversationId,
            Status = status,
            IsPlaceholder = isPlaceholder,
            AssistantMessage = assistantMessage,
            ToolCalls = [],
            Citations = [],
            StructuredOutput = StructuredAnswerFactory.Create(assistantMessage, [], reason, followUpSuggestions),
            Confidence = new ConfidenceIndicatorDto
            {
                Level = ConfidenceIndicatorDto.NotFound,
                EvidenceCoverage = 0
            },
            Usage = usage
        };
    }
}

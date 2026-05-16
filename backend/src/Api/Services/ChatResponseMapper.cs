using Api.Contracts;
using Api.Models;

namespace Api.Services;

public static class ChatResponseMapper
{
    public static ChatResponse FromMatches(
        string conversationId,
        string status,
        bool isPlaceholder,
        string assistantMessage,
        IReadOnlyList<VectorSearchMatch> matches,
        ChatUsageDto usage,
        string? notFoundReason = null)
    {
        var citedChunkIds = matches.Select(m => m.Record.Id).ToList();

        return new ChatResponse
        {
            ConversationId = conversationId,
            Status = status,
            IsPlaceholder = isPlaceholder,
            AssistantMessage = assistantMessage,
            ToolCalls = [],
            Citations = matches.Select(CitationMapper.FromMatch).ToList(),
            StructuredOutput = StructuredAnswerFactory.Create(assistantMessage, citedChunkIds, notFoundReason),
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
        ChatUsageDto usage)
    {
        return new ChatResponse
        {
            ConversationId = conversationId,
            Status = status,
            IsPlaceholder = isPlaceholder,
            AssistantMessage = assistantMessage,
            ToolCalls = [],
            Citations = [],
            StructuredOutput = StructuredAnswerFactory.Create(assistantMessage, [], reason),
            Confidence = new ConfidenceIndicatorDto
            {
                Level = ConfidenceIndicatorDto.NotFound,
                EvidenceCoverage = 0
            },
            Usage = usage
        };
    }
}

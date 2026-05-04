using Api.Contracts;

namespace Api.Services;

public sealed class FallbackRetrievalChatService(
    IEmbeddingService embeddingService,
    IVectorStoreService vectorStoreService) : IRetrievalChatService
{
    public async Task<ChatResponse> GenerateResponseAsync(ChatRequest request, CancellationToken cancellationToken = default)
    {
        var latestUserMessage = request.Messages.LastOrDefault(m =>
            m.Role.Equals("user", StringComparison.OrdinalIgnoreCase))?.Content ?? string.Empty;

        var queryEmbedding = await embeddingService.EmbedAsync(latestUserMessage, cancellationToken);
        var matches = await vectorStoreService.SearchAsync(queryEmbedding, topK: 3, cancellationToken);

        var lowerMessage = latestUserMessage.ToLowerInvariant();
        var asksForHours = lowerMessage.Contains("hour") ||
            lowerMessage.Contains("open") ||
            lowerMessage.Contains("close") ||
            lowerMessage.Contains("schedule");

        var answer = asksForHours
            ? "Monday - Friday: 6:00 AM - 11:00 PM\nSaturday: 7:00 AM - 11:00 PM\nSunday: 7:00 AM - 10:00 PM"
            : BuildContextualAnswer(matches);

        return new ChatResponse
        {
            ConversationId = request.ConversationId,
            Status = "success",
            IsPlaceholder = true,
            AssistantMessage = answer,
            ToolCalls = asksForHours ? ["get_store_hours"] : [],
            Citations = matches
                .Select(m => new CitationDto
                {
                    Source = m.Record.Source,
                    Snippet = m.Record.ChunkText.Length > 200
                        ? m.Record.ChunkText[..200] + "..."
                        : m.Record.ChunkText
                })
                .ToList()
        };
    }

    private static string BuildContextualAnswer(IReadOnlyList<Api.Models.VectorSearchMatch> matches)
    {
        if (matches.Count == 0)
        {
            return "I could not find relevant SOP context yet. Run ingest first and try again.";
        }

        var best = matches[0].Record.ChunkText;
        var snippet = best.Length > 500 ? best[..500] + "..." : best;

        return $"Based on the SOP, this is the most relevant guidance:\n\n{snippet}";
    }
}

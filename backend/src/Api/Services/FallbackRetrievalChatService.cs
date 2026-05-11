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
        var answer = BuildContextualAnswer(matches);

        return new ChatResponse
        {
            ConversationId = request.ConversationId,
            Status = "success",
            IsPlaceholder = true,
            AssistantMessage = answer,
            ToolCalls = [],
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

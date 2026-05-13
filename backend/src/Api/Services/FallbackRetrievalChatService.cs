using Api.Contracts;

namespace Api.Services;

public sealed class FallbackRetrievalChatService(
    IEmbeddingService embeddingService,
    IVectorStoreService vectorStoreService,
    IConfiguration configuration) : IRetrievalChatService
{
    private readonly int _retrievalTopK = Math.Max(1, configuration.GetValue<int?>("Retrieval:TopK") ?? 3);
    private readonly double _minSimilarityScore = Math.Clamp(configuration.GetValue<double?>("Retrieval:MinSimilarityScore") ?? 0.3, 0.0, 1.0);
    private const string NoRelevantContextMessage =
        "I could not find enough relevant information in the SOP to answer that question.";

    public async Task<ChatResponse> GenerateResponseAsync(ChatRequest request, CancellationToken cancellationToken = default)
    {
        var latestUserMessage = request.Messages.LastOrDefault(m =>
            m.Role.Equals("user", StringComparison.OrdinalIgnoreCase))?.Content ?? string.Empty;

        var queryEmbedding = await embeddingService.EmbedAsync(latestUserMessage, cancellationToken);
        var rawMatches = await vectorStoreService.SearchAsync(queryEmbedding, topK: _retrievalTopK, cancellationToken);
        var matches = rawMatches.Where(m => m.Score >= _minSimilarityScore).ToList();
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
            return NoRelevantContextMessage;
        }

        var best = matches[0].Record.ChunkText;
        var snippet = best.Length > 500 ? best[..500] + "..." : best;

        return $"Based on the SOP, this is the most relevant guidance:\n\n{snippet}";
    }
}

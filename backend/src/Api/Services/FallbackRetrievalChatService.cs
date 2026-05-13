using Api.Contracts;
using Api.Options;
using Microsoft.Extensions.Options;
using System.Diagnostics;

namespace Api.Services;

public sealed class FallbackRetrievalChatService(
    IEmbeddingService embeddingService,
    IVectorStoreService vectorStoreService,
    IOptions<RetrievalOptions> options,
    ILogger<FallbackRetrievalChatService> logger) : IRetrievalChatService
{
    private readonly int _retrievalTopK = Math.Max(1, options.Value.TopK);
    private readonly double _minSimilarityScore = Math.Clamp(options.Value.MinSimilarityScore, 0.0, 1.0);
    private const string NoRelevantContextMessage =
        "I could not find enough relevant information in the SOP to answer that question.";

    public async Task<ChatResponse> GenerateResponseAsync(ChatRequest request, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var latestUserMessage = request.Messages.LastOrDefault(m =>
            m.Role.Equals("user", StringComparison.OrdinalIgnoreCase))?.Content ?? string.Empty;

        var queryEmbedding = await embeddingService.EmbedAsync(latestUserMessage, cancellationToken);
        var rawMatches = await vectorStoreService.SearchAsync(queryEmbedding, topK: _retrievalTopK, cancellationToken);
        var matches = rawMatches.Where(m => m.Score >= _minSimilarityScore).ToList();

        if (rawMatches.Count == 0)
        {
            logger.LogWarning(
                "Fallback retrieval found no vector candidates. ConversationId={ConversationId}",
                request.ConversationId);
        }

        var answer = BuildContextualAnswer(matches);
        stopwatch.Stop();

        logger.LogInformation(
            "Fallback chat response generated. ConversationId={ConversationId}, Mode={Mode}, TopK={TopK}, Threshold={Threshold}, RetrievedChunkIds={ChunkIds}, RetrievedScores={Scores}, TotalLatencyMs={TotalLatencyMs}",
            request.ConversationId,
            "fallback",
            _retrievalTopK,
            _minSimilarityScore,
            string.Join(",", matches.Select(m => m.Record.Id)),
            string.Join(",", matches.Select(m => m.Score.ToString("F3"))),
            stopwatch.ElapsedMilliseconds);

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

using Api.Contracts;
using Api.Models;
using Api.Observability;
using Api.Options;
using Microsoft.Extensions.Options;
using System.Diagnostics;

namespace Api.Services;

public sealed class FallbackRetrievalChatService(
    IEmbeddingService embeddingService,
    IVectorStoreService vectorStoreService,
    IRetrievalReranker reranker,
    IUserQueryGuardrailService guardrailService,
    OpenAIUsageTracker usageTracker,
    IOptions<RetrievalOptions> options,
    ILogger<FallbackRetrievalChatService> logger) : IRetrievalChatService
{
    private readonly int _retrievalTopK = Math.Max(1, options.Value.TopK);
    private readonly bool _enableReranking = options.Value.EnableReranking;
    private readonly int _rerankCandidateMultiplier = Math.Max(1, options.Value.RerankCandidateMultiplier);
    private readonly double _minSimilarityScore = Math.Clamp(options.Value.MinSimilarityScore, 0.0, 1.0);
    private readonly bool _enableQueryRewriting = options.Value.EnableQueryRewriting;
    private const string NoRelevantContextMessage =
        "I could not find enough relevant information in the SOP to answer that question.";

    public async Task<ChatResponse> GenerateResponseAsync(ChatRequest request, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var knowledgeBaseId = KnowledgeBaseScope.Normalize(request.KnowledgeBaseId);
        using var activity = AppTelemetry.ActivitySource.StartActivity("chat.fallback.generate");
        activity?.SetTag("chat.mode", "fallback");
        activity?.SetTag("chat.knowledge_base_id", knowledgeBaseId);
        AppTelemetry.ChatRequests.Add(1);

        var latestUserMessage = request.Messages.LastOrDefault(m =>
            m.Role.Equals("user", StringComparison.OrdinalIgnoreCase))?.Content ?? string.Empty;
        var guardrailDecision = guardrailService.Evaluate(latestUserMessage);

        if (guardrailDecision.IsEscalated)
        {
            logger.LogWarning(
                "Guardrail escalation triggered. ConversationId={ConversationId}, Mode={Mode}, Category={Category}",
                request.ConversationId,
                "fallback",
                guardrailDecision.Category);

            return BuildGuardrailResponse(request, guardrailDecision);
        }

        var (queryText, wasRewritten) = QueryRewriteHeuristics.Rewrite(request.Messages, _enableQueryRewriting);

        logger.LogInformation(
            "Fallback retrieval query prepared. ConversationId={ConversationId}, KnowledgeBaseId={KnowledgeBaseId}, QueryRewritingEnabled={QueryRewritingEnabled}, QueryWasRewritten={QueryWasRewritten}, OriginalUserMessageLength={OriginalUserMessageLength}, RetrievalQueryLength={RetrievalQueryLength}",
            request.ConversationId,
            knowledgeBaseId,
            _enableQueryRewriting,
            wasRewritten,
            request.Messages.LastOrDefault(m => m.Role.Equals("user", StringComparison.OrdinalIgnoreCase))?.Content?.Length ?? 0,
            queryText.Length);

        var queryEmbedding = await embeddingService.EmbedAsync(queryText, cancellationToken);
        var candidateTopK = _enableReranking ? _retrievalTopK * _rerankCandidateMultiplier : _retrievalTopK;
        var rawMatches = await vectorStoreService.SearchAsync(
            queryEmbedding,
            topK: candidateTopK,
            KnowledgeBaseScope.BuildMetadataFilter(knowledgeBaseId),
            cancellationToken);
        var scoredMatches = _enableReranking
            ? reranker.Rerank(queryText, rawMatches, _retrievalTopK)
            : rawMatches.Take(_retrievalTopK).ToList();
        var matches = scoredMatches.Where(m => m.Score >= _minSimilarityScore).ToList();

        if (rawMatches.Count == 0)
        {
            logger.LogWarning(
                "Fallback retrieval found no vector candidates. ConversationId={ConversationId}",
                request.ConversationId);
        }

        var answer = BuildContextualAnswer(matches);
        stopwatch.Stop();
        AppTelemetry.ChatLatencyMs.Record(stopwatch.Elapsed.TotalMilliseconds);
        activity?.SetTag("chat.total_ms", stopwatch.Elapsed.TotalMilliseconds);
        activity?.SetTag("chat.citation_count", matches.Count);

        logger.LogInformation(
            "Fallback chat response generated. ConversationId={ConversationId}, Mode={Mode}, KnowledgeBaseId={KnowledgeBaseId}, TopK={TopK}, Threshold={Threshold}, RerankingEnabled={RerankingEnabled}, Reranker={Reranker}, RetrievedChunkIds={ChunkIds}, RetrievedScores={Scores}, TotalLatencyMs={TotalLatencyMs}",
            request.ConversationId,
            "fallback",
            knowledgeBaseId,
            _retrievalTopK,
            _minSimilarityScore,
            _enableReranking,
            reranker.Name,
            string.Join(",", matches.Select(m => m.Record.Id)),
            string.Join(",", matches.Select(m => m.Score.ToString("F3"))),
            stopwatch.ElapsedMilliseconds);

        return ChatResponseMapper.FromMatches(
            conversationId: request.ConversationId,
            status: "success",
            isPlaceholder: true,
            assistantMessage: answer,
            matches: matches,
            usage: usageTracker.BuildEstimated(
                model: "fallback",
                promptText: string.Join("\n", request.Messages.Select(m => $"{m.Role}: {m.Content}")),
                completionText: answer,
                embeddingText: queryText,
                source: "fallback",
                isExternalCost: false),
            notFoundReason: matches.Count == 0 ? StructuredAnswerDto.NotFoundReason : null);
    }

    private ChatResponse BuildGuardrailResponse(ChatRequest request, GuardrailDecision decision)
    {
        var refusalReason = $"guardrail_{decision.Category}";

        return ChatResponseMapper.NoContext(
            conversationId: request.ConversationId,
            status: "success",
            isPlaceholder: false,
            assistantMessage: decision.EscalationMessage,
            reason: refusalReason,
            usage: usageTracker.BuildEstimated(
                model: "fallback",
                promptText: string.Join("\n", request.Messages.Select(m => $"{m.Role}: {m.Content}")),
                completionText: decision.EscalationMessage,
                source: "guardrail",
                isExternalCost: false));
    }

    private static string BuildContextualAnswer(IReadOnlyList<VectorSearchMatch> matches)
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

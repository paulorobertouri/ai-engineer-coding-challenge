using Api.Contracts;
using Api.Models;
using Api.Observability;
using Api.Options;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Collections.Generic;

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
    private const string NoRelevantContextMessageEs =
        "No encontre suficiente informacion relevante en el SOP para responder esa pregunta.";
    private const string NoRelevantContextMessagePtBr =
        "Nao encontrei informacao relevante suficiente no SOP para responder essa pergunta.";
    private const string NoRelevantContextMessageFr =
        "Je n'ai pas trouve assez d'informations pertinentes dans le SOP pour repondre a cette question.";

    public async Task<ChatResponse> GenerateResponseAsync(ChatRequest request, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var knowledgeBaseId = KnowledgeBaseScope.Normalize(request.KnowledgeBaseId);
        using var logScope = logger.BeginScope(new Dictionary<string, object>
        {
            ["ConversationId"] = request.ConversationId,
            ["KnowledgeBaseId"] = knowledgeBaseId,
            ["ProviderMode"] = "fallback"
        });
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
        var effectiveTopK = ResolveEffectiveTopK(request, queryText);
        var candidateTopK = _enableReranking ? effectiveTopK * _rerankCandidateMultiplier : effectiveTopK;
        var rawMatches = await vectorStoreService.SearchAsync(
            queryEmbedding,
            topK: candidateTopK,
            KnowledgeBaseScope.BuildMetadataFilter(knowledgeBaseId),
            cancellationToken);
        var scoredMatches = _enableReranking
            ? reranker.Rerank(queryText, rawMatches, effectiveTopK)
            : rawMatches.Take(effectiveTopK).ToList();
        var matches = scoredMatches.Where(m => m.Score >= _minSimilarityScore).ToList();

        if (rawMatches.Count == 0)
        {
            logger.LogWarning(
                "Fallback retrieval found no vector candidates. ConversationId={ConversationId}",
                request.ConversationId);
        }

        var answer = BuildContextualAnswer(
            matches,
            request.UserRole,
            request.ResponseLanguage,
            request.ResponseTone,
            request.ResponseLength,
            request.ResponseFormat);
        stopwatch.Stop();
        AppTelemetry.ChatLatencyMs.Record(stopwatch.Elapsed.TotalMilliseconds);
        activity?.SetTag("chat.total_ms", stopwatch.Elapsed.TotalMilliseconds);
        activity?.SetTag("chat.citation_count", matches.Count);

        logger.LogInformation(
            "Fallback chat response generated. ConversationId={ConversationId}, Mode={Mode}, KnowledgeBaseId={KnowledgeBaseId}, BaseTopK={BaseTopK}, EffectiveTopK={EffectiveTopK}, Threshold={Threshold}, RerankingEnabled={RerankingEnabled}, Reranker={Reranker}, RetrievedChunkIds={ChunkIds}, RetrievedScores={Scores}, TotalLatencyMs={TotalLatencyMs}",
            request.ConversationId,
            "fallback",
            knowledgeBaseId,
            _retrievalTopK,
            effectiveTopK,
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
            userRole: request.UserRole,
            responseLanguage: request.ResponseLanguage,
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
                isExternalCost: false),
            userRole: request.UserRole,
            responseLanguage: request.ResponseLanguage);
    }

    private static string BuildContextualAnswer(
        IReadOnlyList<VectorSearchMatch> matches,
        string? userRole,
        string? responseLanguage,
        string? responseTone,
        string? responseLength,
        string? responseFormat)
    {
        var language = NormalizeResponseLanguage(responseLanguage);

        if (matches.Count == 0)
        {
            return GetNoContextMessage(language);
        }

        var best = matches[0].Record.ChunkText;
        var maxLength = responseLength switch
        {
            "short" => 220,
            "long" => 900,
            _ => 500
        };
        var snippet = best.Length > maxLength ? best[..maxLength] + "..." : best;

        var rolePrefix = (userRole, language) switch
        {
            ("manager", "es") => "Vista de gerente: ",
            ("department_lead", "es") => "Vista de lider de departamento: ",
            ("cashier", "es") => "Vista de cajero: ",
            ("manager", "pt-BR") => "Visao do gerente: ",
            ("department_lead", "pt-BR") => "Visao do lider de departamento: ",
            ("cashier", "pt-BR") => "Visao de caixa: ",
            ("manager", "fr") => "Vue manager: ",
            ("department_lead", "fr") => "Vue chef de departement: ",
            ("cashier", "fr") => "Vue caissier: ",
            ("manager", _) => "Manager view: ",
            ("department_lead", _) => "Department lead view: ",
            ("cashier", _) => "Cashier view: ",
            _ => string.Empty
        };

        var leadIn = language switch
        {
            "es" => "Basado en el SOP, esta es la guia mas relevante:",
            "pt-BR" => "Com base no SOP, esta e a orientacao mais relevante:",
            "fr" => "D'apres le SOP, voici la consigne la plus pertinente:",
            _ => "Based on the SOP, this is the most relevant guidance:"
        };

        var tonePrefix = responseTone switch
        {
            "friendly" => language switch
            {
                "es" => "Claro, aqui tienes una guia rapida. ",
                "pt-BR" => "Claro, aqui vai uma orientacao rapida. ",
                "fr" => "Bien sur, voici un guide rapide. ",
                _ => "Sure, here is a quick guide. "
            },
            "formal" => language switch
            {
                "es" => "Conforme al SOP, ",
                "pt-BR" => "Conforme o SOP, ",
                "fr" => "Conformement au SOP, ",
                _ => "According to the SOP, "
            },
            _ => string.Empty
        };

        var paragraphAnswer = $"{tonePrefix}{rolePrefix}{leadIn}\n\n{snippet}";

        if (string.Equals(responseFormat, "bullets", StringComparison.Ordinal))
        {
            return $"{tonePrefix}{rolePrefix}{leadIn}\n\n- {snippet}";
        }

        if (string.Equals(responseFormat, "checklist", StringComparison.Ordinal))
        {
            return $"{tonePrefix}{rolePrefix}{leadIn}\n\n[ ] {snippet}";
        }

        return paragraphAnswer;
    }

    private static string NormalizeResponseLanguage(string? responseLanguage)
    {
        if (string.IsNullOrWhiteSpace(responseLanguage))
        {
            return "en";
        }

        return responseLanguage.Trim();
    }

    private static string GetNoContextMessage(string language)
    {
        return language switch
        {
            "es" => NoRelevantContextMessageEs,
            "pt-BR" => NoRelevantContextMessagePtBr,
            "fr" => NoRelevantContextMessageFr,
            _ => NoRelevantContextMessage
        };
    }

    private int ResolveEffectiveTopK(ChatRequest request, string retrievalQuery)
    {
        var userTurnCount = request.Messages.Count(message =>
            string.Equals(message.Role, "user", StringComparison.OrdinalIgnoreCase));

        var complexityBoost = 0;
        if (retrievalQuery.Length >= 160)
        {
            complexityBoost += 1;
        }

        if (userTurnCount >= 3)
        {
            complexityBoost += 1;
        }

        return Math.Clamp(_retrievalTopK + complexityBoost, 1, _retrievalTopK + 2);
    }
}

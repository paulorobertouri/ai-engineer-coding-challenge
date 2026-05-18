using Api.Contracts;
using Api.Models;
using Api.Observability;
using Api.Services;
using Api.Options;
using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Chat;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using System.Diagnostics;
using System.Collections.Generic;

namespace Api.Services;

public sealed class OpenAIRetrievalChatService(
    OpenAIClient openAiClient,
    IOptions<OpenAIOptions> openAiOptions,
    IOptions<RetrievalOptions> retrievalOptions,
    OpenAIRetrievalChatServiceDependencies dependencies) : IRetrievalChatService
{
    private readonly string _chatModel = openAiOptions.Value.ChatModel;
    private readonly int _retrievalTopK = Math.Max(1, retrievalOptions.Value.TopK);
    private readonly bool _enableReranking = retrievalOptions.Value.EnableReranking;
    private readonly int _rerankCandidateMultiplier = Math.Max(1, retrievalOptions.Value.RerankCandidateMultiplier);
    private readonly double _minSimilarityScore = Math.Clamp(retrievalOptions.Value.MinSimilarityScore, 0.0, 1.0);
    private readonly bool _enableQueryRewriting = retrievalOptions.Value.EnableQueryRewriting;
    private readonly bool _enableTools = ToolCallingPolicy.IsEnabled(openAiOptions.Value);
    private readonly ResiliencePipeline _resiliencePipeline = BuildPipeline(openAiOptions.Value, dependencies.Logger);
    private readonly OpenAIRetrievalChatServiceDependencies _dependencies = dependencies;

    private static readonly string[] jsonSerializable = new[] { "query" };
    private const string NoRelevantContextMessage =
        "I could not find enough relevant information in the SOP to answer that question.";
    private const string OpenAiMode = "openai";
    private const string ProviderUnavailableMessage = "The AI provider is temporarily unavailable. Please retry shortly.";

    private static ResiliencePipeline BuildPipeline(OpenAIOptions options, ILogger<OpenAIRetrievalChatService> logger)
    {
        var builder = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                // Do not retry on cancellation — the caller intentionally stopped the request
                ShouldHandle = new PredicateBuilder().Handle<Exception>(ex => ex is not OperationCanceledException),
                Delay = TimeSpan.FromSeconds(2),
                MaxRetryAttempts = 3,
                BackoffType = DelayBackoffType.Exponential,
                OnRetry = args =>
                {
                    logger.LogWarning("Retrying OpenAI call. Attempt: {AttemptNumber}", args.AttemptNumber);
                    return default;
                }
            })
            .AddTimeout(TimeSpan.FromSeconds(30));

        if (options.CircuitBreaker.Enabled)
        {
            builder.AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                ShouldHandle = new PredicateBuilder().Handle<Exception>(ex => ex is not OperationCanceledException),
                FailureRatio = options.CircuitBreaker.FailureRatio,
                MinimumThroughput = options.CircuitBreaker.MinimumThroughput,
                SamplingDuration = TimeSpan.FromSeconds(options.CircuitBreaker.SamplingDurationSeconds),
                BreakDuration = TimeSpan.FromSeconds(options.CircuitBreaker.BreakDurationSeconds),
                OnOpened = args =>
                {
                    logger.LogError(
                        "OpenAI circuit breaker opened. Failures exceeded threshold. RetryAfterMs={RetryAfterMs}",
                        args.BreakDuration.TotalMilliseconds);
                    return default;
                },
                OnClosed = args =>
                {
                    logger.LogInformation("OpenAI circuit breaker closed. Normal call flow resumed.");
                    return default;
                },
                OnHalfOpened = args =>
                {
                    logger.LogWarning("OpenAI circuit breaker half-open. Probing provider availability.");
                    return default;
                }
            });
        }

        return builder.Build();
    }

    public Task<ChatResponse> GenerateResponseAsync(ChatRequest request, CancellationToken cancellationToken = default) =>
        GenerateResponseWithCircuitBreakerAsync(request, cancellationToken);

    private async Task<ChatResponse> GenerateResponseWithCircuitBreakerAsync(ChatRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return await _resiliencePipeline.ExecuteAsync(async ct =>
            {
                var totalStopwatch = Stopwatch.StartNew();
                var knowledgeBaseId = KnowledgeBaseScope.Normalize(request.KnowledgeBaseId);
                using var logScope = _dependencies.Logger.BeginScope(new Dictionary<string, object>
                {
                    ["ConversationId"] = request.ConversationId,
                    ["KnowledgeBaseId"] = knowledgeBaseId,
                    ["ProviderMode"] = OpenAiMode
                });
                using var activity = AppTelemetry.ActivitySource.StartActivity("chat.openai.generate");
                activity?.SetTag("chat.mode", OpenAiMode);
                activity?.SetTag("chat.knowledge_base_id", knowledgeBaseId);
                activity?.SetTag("chat.model", _chatModel);
                AppTelemetry.ChatRequests.Add(1);

                var latestUserMessage = request.Messages
                    .LastOrDefault(m => m.Role.Equals("user", StringComparison.OrdinalIgnoreCase))?.Content ?? string.Empty;
                var guardrailDecision = _dependencies.GuardrailService.Evaluate(latestUserMessage);

                if (guardrailDecision.IsEscalated)
                {
                    _dependencies.Logger.LogWarning(
                        "Guardrail escalation triggered. ConversationId={ConversationId}, Mode={Mode}, Category={Category}",
                        request.ConversationId,
                        OpenAiMode,
                        guardrailDecision.Category);

                    return BuildGuardrailResponse(request, guardrailDecision, _dependencies.UsageTracker);
                }

                var (retrievalQuery, matches) = await RetrieveMatchesAsync(
                    request,
                    knowledgeBaseId,
                    latestUserMessage,
                    ct);

                if (matches.Count == 0)
                {
                    return BuildNotFoundResponse(request, _dependencies.UsageTracker, retrievalQuery);
                }

                return await GenerateCompletionResponseAsync(
                    request,
                    knowledgeBaseId,
                    retrievalQuery,
                    matches,
                    totalStopwatch,
                    activity,
                    ct);
            }, cancellationToken);
        }
        catch (BrokenCircuitException ex)
        {
            AppTelemetry.ChatFailures.Add(1);
            _dependencies.Logger.LogError(
                ex,
                "OpenAI request rejected because circuit breaker is open. ConversationId={ConversationId}",
                request.ConversationId);

            return ChatResponseMapper.NoContext(
                conversationId: request.ConversationId,
                status: "error",
                isPlaceholder: false,
                assistantMessage: ProviderUnavailableMessage,
                reason: "provider_circuit_open",
                usage: _dependencies.UsageTracker.BuildEstimated(
                    model: _chatModel,
                    promptText: string.Join("\n", request.Messages.Select(m => $"{m.Role}: {m.Content}")),
                    completionText: string.Empty,
                    source: "circuit_breaker",
                    isExternalCost: false),
                userRole: request.UserRole,
                responseLanguage: request.ResponseLanguage);
        }
    }

    private async Task<(string RetrievalQuery, IReadOnlyList<VectorSearchMatch> Matches)> RetrieveMatchesAsync(
        ChatRequest request,
        string knowledgeBaseId,
        string latestUserMessage,
        CancellationToken ct)
    {
        var (retrievalQuery, wasRewritten) = QueryRewriteHeuristics.Rewrite(request.Messages, _enableQueryRewriting);

        _dependencies.Logger.LogDebug(
            "OpenAI retrieval query prepared. ConversationId={ConversationId}, KnowledgeBaseId={KnowledgeBaseId}, QueryRewritingEnabled={QueryRewritingEnabled}, QueryWasRewritten={QueryWasRewritten}, OriginalUserMessageLength={OriginalUserMessageLength}, RetrievalQueryLength={RetrievalQueryLength}",
            request.ConversationId,
            knowledgeBaseId,
            _enableQueryRewriting,
            wasRewritten,
            latestUserMessage.Length,
            retrievalQuery.Length);

        var queryEmbedding = await _dependencies.EmbeddingService.EmbedAsync(retrievalQuery, ct);
        var effectiveTopK = ResolveEffectiveTopK(request, retrievalQuery);
        var candidateTopK = _enableReranking ? effectiveTopK * _rerankCandidateMultiplier : effectiveTopK;
        var rawMatches = await _dependencies.VectorStoreService.SearchAsync(
            queryEmbedding,
            topK: candidateTopK,
            KnowledgeBaseScope.BuildMetadataFilter(knowledgeBaseId),
            ct);
        var scoredMatches = _enableReranking
            ? _dependencies.Reranker.Rerank(retrievalQuery, rawMatches, effectiveTopK)
            : rawMatches.Take(effectiveTopK).ToList();
        var matches = FilterMatches(scoredMatches);

        _dependencies.Logger.LogInformation(
            "RAG retrieval completed. KnowledgeBaseId={KnowledgeBaseId}, BaseTopK={BaseTopK}, EffectiveTopK={EffectiveTopK}, Threshold={Threshold}, RawCount={RawCount}, FilteredCount={FilteredCount}, Scores={Scores}",
            knowledgeBaseId,
            _retrievalTopK,
            effectiveTopK,
            _minSimilarityScore,
            rawMatches.Count,
            matches.Count,
            string.Join(",", scoredMatches.Select(m => m.Score.ToString("F3"))));

        if (rawMatches.Count == 0)
        {
            _dependencies.Logger.LogWarning(
                "Vector store returned no candidates. ConversationId={ConversationId}, Model={Model}",
                request.ConversationId,
                _chatModel);
        }

        if (matches.Count == 0)
        {
            _dependencies.Logger.LogWarning(
                "No relevant SOP context found above threshold {Threshold} for conversation {ConversationId}.",
                _minSimilarityScore,
                request.ConversationId);
        }

        return (retrievalQuery, matches);
    }

    private async Task<ChatResponse> GenerateCompletionResponseAsync(
        ChatRequest request,
        string knowledgeBaseId,
        string retrievalQuery,
        IReadOnlyList<VectorSearchMatch> matches,
        Stopwatch totalStopwatch,
        Activity? activity,
        CancellationToken ct)
    {
        var messages = BuildChatMessages(request, matches);

        var options = new ChatCompletionOptions();
        if (_enableTools)
        {
            foreach (var tool in BuildToolDefinitions()) options.Tools.Add(tool);
        }
        else
        {
            _dependencies.Logger.LogDebug(
                "Tool calling is disabled by server configuration. ConversationId={ConversationId}",
                request.ConversationId);
        }

        var client = openAiClient.GetChatClient(_chatModel);
        var completionStopwatch = Stopwatch.StartNew();
        var response = await client.CompleteChatAsync(messages, options, ct);
        completionStopwatch.Stop();
        AppTelemetry.OpenAiCallLatencyMs.Record(completionStopwatch.Elapsed.TotalMilliseconds);
        var chatCompletion = response.Value;

        if (_enableTools && chatCompletion.FinishReason == ChatFinishReason.ToolCalls)
        {
            matches = await HandleToolCallsAsync(
                chatCompletion,
                messages,
                matches,
                request.ConversationId,
                knowledgeBaseId,
                ct);
            completionStopwatch.Restart();
            response = await client.CompleteChatAsync(messages, options, ct);
            completionStopwatch.Stop();
            AppTelemetry.OpenAiCallLatencyMs.Record(completionStopwatch.Elapsed.TotalMilliseconds);
            chatCompletion = response.Value;
        }

        totalStopwatch.Stop();
        AppTelemetry.ChatLatencyMs.Record(totalStopwatch.Elapsed.TotalMilliseconds);
        activity?.SetTag("chat.total_ms", totalStopwatch.Elapsed.TotalMilliseconds);
        activity?.SetTag("chat.citation_count", matches.Count);

        _dependencies.Logger.LogInformation(
            "Chat response generated. ConversationId={ConversationId}, Model={Model}, Mode={Mode}, KnowledgeBaseId={KnowledgeBaseId}, ToolingEnabled={ToolingEnabled}, RerankingEnabled={RerankingEnabled}, Reranker={Reranker}, RetrievedChunkIds={ChunkIds}, RetrievedScores={Scores}, CompletionLatencyMs={CompletionLatencyMs}, TotalLatencyMs={TotalLatencyMs}",
            request.ConversationId,
            _chatModel,
            OpenAiMode,
            knowledgeBaseId,
            _enableTools,
            _enableReranking,
            _dependencies.Reranker.Name,
            string.Join(",", matches.Select(m => m.Record.Id)),
            string.Join(",", matches.Select(m => m.Score.ToString("F3"))),
            completionStopwatch.ElapsedMilliseconds,
            totalStopwatch.ElapsedMilliseconds);

        return BuildChatResponse(request, chatCompletion, matches, _dependencies.UsageTracker, retrievalQuery, _chatModel);
    }

    private IReadOnlyList<VectorSearchMatch> FilterMatches(IReadOnlyList<VectorSearchMatch> matches) =>
        matches.Where(m => m.Score >= _minSimilarityScore).ToList();

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

    private static List<ChatMessage> BuildChatMessages(ChatRequest request, IEnumerable<VectorSearchMatch> matches)
    {
        var contextText = string.Join("\n\n", matches.Select(m => $"Source: {m.Record.Source}\nContent: {m.Record.ChunkText}"));
        var responseLanguage = NormalizeResponseLanguage(request.ResponseLanguage);
        var styleInstruction = BuildStyleInstruction(request);
        var roleInstruction = request.UserRole switch
        {
            "manager" => "User role is manager. Emphasize policy rationale, compliance implications, and escalation paths.",
            "department_lead" => "User role is department_lead. Emphasize team execution details, checklists, and handoff responsibilities.",
            "cashier" => "User role is cashier. Emphasize practical counter-level steps and immediate actions.",
            _ => ""
        };
        var languageInstruction = responseLanguage == "en"
            ? "Respond in English."
            : $"Respond in {responseLanguage}. Preserve source names, citation snippets, and identifiers exactly as provided in the SOP context.";
        var messages = new List<ChatMessage>
        {
            ChatMessage.CreateSystemMessage($"""
                You are a helpful assistant for a grocery store chain. 
                Answer the user's questions based ONLY on the provided SOP context.
                If you don't know the answer, say you don't know based on the SOP.
                Always provide helpful and concise answers.
                The user may ask in any language, and the SOP context may be in a different language.
                {languageInstruction}
                {styleInstruction}

                {roleInstruction}

                CONTEXT:
                {contextText}
                """)
        };

        foreach (var msg in request.Messages)
        {
            if (msg.Role.Equals("user", StringComparison.OrdinalIgnoreCase))
                messages.Add(ChatMessage.CreateUserMessage(msg.Content));
            else if (msg.Role.Equals("assistant", StringComparison.OrdinalIgnoreCase))
                messages.Add(ChatMessage.CreateAssistantMessage(msg.Content));
        }

        return messages;
    }

    private static string NormalizeResponseLanguage(string? responseLanguage)
    {
        if (string.IsNullOrWhiteSpace(responseLanguage))
        {
            return "en";
        }

        return responseLanguage.Trim();
    }

    private static string BuildStyleInstruction(ChatRequest request)
    {
        var toneInstruction = request.ResponseTone switch
        {
            "formal" => "Use a formal and policy-oriented tone.",
            "friendly" => "Use a friendly and approachable tone.",
            _ => "Use a neutral professional tone."
        };

        var lengthInstruction = request.ResponseLength switch
        {
            "short" => "Keep the answer short (about 2-4 sentences).",
            "long" => "Provide an extended answer with clear detail and rationale.",
            _ => "Use a medium-length answer with concise detail."
        };

        var formatInstruction = request.ResponseFormat switch
        {
            "bullets" => "Format the answer as bullet points.",
            "checklist" => "Format the answer as a checklist with actionable items.",
            _ => "Format the answer as a paragraph unless list formatting is clearly better."
        };

        return $"{toneInstruction} {lengthInstruction} {formatInstruction}";
    }

    private static List<ChatTool> BuildToolDefinitions() =>
    [
        ChatTool.CreateFunctionTool(
            "search_sop",
            "Searches the SOP for more information using a specific query.",
            BinaryData.FromObjectAsJson(new
            {
                type = "object",
                properties = new
                {
                    query = new { type = "string", description = "The search query to look up in the SOP." }
                },
                required = jsonSerializable
            })
        )
    ];

    private async Task<IReadOnlyList<VectorSearchMatch>> HandleToolCallsAsync(
        ChatCompletion chatCompletion,
        List<ChatMessage> messages,
        IReadOnlyList<VectorSearchMatch> matches,
        string conversationId,
        string knowledgeBaseId,
        CancellationToken ct)
    {
        messages.Add(ChatMessage.CreateAssistantMessage(chatCompletion));
        var allMatches = matches.ToList();

        foreach (var toolCall in chatCompletion.ToolCalls)
        {
            await HandleSearchSopToolCallAsync(
                toolCall,
                messages,
                allMatches,
                conversationId,
                knowledgeBaseId,
                ct);
        }

        return allMatches;
    }

    private async Task HandleSearchSopToolCallAsync(
        ChatToolCall toolCall,
        List<ChatMessage> messages,
        List<VectorSearchMatch> allMatches,
        string conversationId,
        string knowledgeBaseId,
        CancellationToken ct)
    {
        var queryParseResult = ToolCallingPolicy.TryExtractSearchQuery(toolCall.FunctionArguments.ToString(), out var query);
        if (queryParseResult == ToolCallQueryParseResult.InvalidJson)
        {
            _dependencies.Logger.LogWarning(
                "Tool call parse failed. ConversationId={ConversationId}, ToolCallId={ToolCallId}, ToolName={ToolName}, Reason=InvalidJson",
                conversationId,
                toolCall.Id,
                toolCall.FunctionName);
            messages.Add(ChatMessage.CreateToolMessage(toolCall.Id, ""));
            return;
        }

        if (queryParseResult == ToolCallQueryParseResult.EmptyQuery)
        {
            _dependencies.Logger.LogWarning(
                "Tool call rejected. ConversationId={ConversationId}, ToolCallId={ToolCallId}, ToolName={ToolName}, Reason=EmptyQuery",
                conversationId,
                toolCall.Id,
                toolCall.FunctionName);
            messages.Add(ChatMessage.CreateToolMessage(toolCall.Id, ""));
            return;
        }

        var toolQueryEmbedding = await _dependencies.EmbeddingService.EmbedAsync(query, ct);
        var rawToolMatches = await _dependencies.VectorStoreService.SearchAsync(
            toolQueryEmbedding,
            topK: _enableReranking ? _retrievalTopK * _rerankCandidateMultiplier : _retrievalTopK,
            KnowledgeBaseScope.BuildMetadataFilter(knowledgeBaseId),
            ct);
        var scoredToolMatches = _enableReranking
            ? _dependencies.Reranker.Rerank(query, rawToolMatches, _retrievalTopK)
            : rawToolMatches.Take(_retrievalTopK).ToList();
        var toolMatches = FilterMatches(scoredToolMatches);

        _dependencies.Logger.LogInformation(
            "Tool retrieval completed. TopK={TopK}, Threshold={Threshold}, RawCount={RawCount}, FilteredCount={FilteredCount}, Scores={Scores}",
            _retrievalTopK,
            _minSimilarityScore,
            rawToolMatches.Count,
            toolMatches.Count,
            string.Join(",", scoredToolMatches.Select(m => m.Score.ToString("F3"))));

        if (toolMatches.Count == 0)
        {
            _dependencies.Logger.LogWarning(
                "Tool retrieval returned no relevant matches. ConversationId={ConversationId}, ToolCallId={ToolCallId}, ToolName={ToolName}, Threshold={Threshold}",
                conversationId,
                toolCall.Id,
                toolCall.FunctionName,
                _minSimilarityScore);
            messages.Add(ChatMessage.CreateToolMessage(toolCall.Id, string.Empty));
            return;
        }

        var toolContext = ToolCallingPolicy.BuildToolContext(toolMatches);
        messages.Add(ChatMessage.CreateToolMessage(toolCall.Id, toolContext));

        allMatches.AddRange(toolMatches.Where(m => allMatches.All(existing => existing.Record.Id != m.Record.Id)));
    }

    private ChatResponse BuildNotFoundResponse(ChatRequest request, OpenAIUsageTracker usageTracker, string retrievalQuery) =>
        ChatResponseMapper.NoContext(
            conversationId: request.ConversationId,
            status: "success",
            isPlaceholder: false,
            assistantMessage: NoRelevantContextMessage,
            reason: StructuredAnswerDto.NotFoundReason,
            usage: usageTracker.BuildEstimated(
                model: _chatModel,
                promptText: string.Empty,
                completionText: string.Empty,
                embeddingText: retrievalQuery,
                source: OpenAiMode,
                isExternalCost: true),
            userRole: request.UserRole,
            responseLanguage: request.ResponseLanguage);

    private ChatResponse BuildGuardrailResponse(ChatRequest request, GuardrailDecision decision, OpenAIUsageTracker usageTracker)
    {
        var refusalReason = $"guardrail_{decision.Category}";

        return ChatResponseMapper.NoContext(
            conversationId: request.ConversationId,
            status: "success",
            isPlaceholder: false,
            assistantMessage: decision.EscalationMessage,
            reason: refusalReason,
            usage: usageTracker.BuildEstimated(
                model: _chatModel,
                promptText: string.Join("\n", request.Messages.Select(m => $"{m.Role}: {m.Content}")),
                completionText: decision.EscalationMessage,
                source: "guardrail",
                isExternalCost: false),
            userRole: request.UserRole,
            responseLanguage: request.ResponseLanguage);
    }

    private static ChatResponse BuildChatResponse(
        ChatRequest request,
        ChatCompletion chatCompletion,
        IEnumerable<VectorSearchMatch> matches,
        OpenAIUsageTracker usageTracker,
        string retrievalQuery,
        string model)
    {
        var matchList = matches.ToList();
        var assistantMessage = chatCompletion.Content.Count > 0 ? chatCompletion.Content[0].Text : string.Empty;
        var promptText = BuildTelemetryPromptText(request, matchList);

        return ChatResponseMapper.FromMatches(
            conversationId: request.ConversationId,
            status: "success",
            isPlaceholder: false,
            assistantMessage: assistantMessage,
            matches: matchList,
            usage: usageTracker.BuildFromSdkOrEstimate(chatCompletion, model, promptText, assistantMessage, retrievalQuery),
            userRole: request.UserRole,
            responseLanguage: request.ResponseLanguage);
    }

    private static string BuildTelemetryPromptText(ChatRequest request, IReadOnlyList<VectorSearchMatch> matches)
    {
        var requestText = string.Join("\n", request.Messages.Select(message => $"{message.Role}: {message.Content}"));
        var contextText = string.Join("\n\n", matches.Select(match => match.Record.ChunkText));
        return string.Join("\n\n", requestText, contextText);
    }
}

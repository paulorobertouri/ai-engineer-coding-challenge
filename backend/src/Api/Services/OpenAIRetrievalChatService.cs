using Api.Contracts;
using Api.Models;
using Api.Services;
using Api.Options;
using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Chat;
using Polly;
using Polly.Retry;
using System.Diagnostics;

namespace Api.Services;

public sealed class OpenAIRetrievalChatService(
    OpenAIClient openAiClient,
    IOptions<OpenAIOptions> openAiOptions,
    IOptions<RetrievalOptions> retrievalOptions,
    IEmbeddingService embeddingService,
    IVectorStoreService vectorStoreService,
    ILogger<OpenAIRetrievalChatService> logger) : IRetrievalChatService
{
    private readonly string _chatModel = openAiOptions.Value.ChatModel;
    private readonly int _retrievalTopK = Math.Max(1, retrievalOptions.Value.TopK);
    private readonly double _minSimilarityScore = Math.Clamp(retrievalOptions.Value.MinSimilarityScore, 0.0, 1.0);
    private readonly bool _enableTools = ToolCallingPolicy.IsEnabled(openAiOptions.Value);
    private readonly ResiliencePipeline _resiliencePipeline = new ResiliencePipelineBuilder()
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
        .AddTimeout(TimeSpan.FromSeconds(30))
        .Build();
    private static readonly string[] jsonSerializable = new[] { "query" };
    private const string NoRelevantContextMessage =
        "I could not find enough relevant information in the SOP to answer that question.";

    public async Task<ChatResponse> GenerateResponseAsync(ChatRequest request, CancellationToken cancellationToken = default)
    {
        return await _resiliencePipeline.ExecuteAsync(async ct =>
        {
            var totalStopwatch = Stopwatch.StartNew();
            var latestUserMessage = request.Messages
                .LastOrDefault(m => m.Role.Equals("user", StringComparison.OrdinalIgnoreCase))?.Content ?? "";

            // 1. Initial Retrieval (Standard RAG)
            var queryEmbedding = await embeddingService.EmbedAsync(latestUserMessage, ct);
            var rawMatches = await vectorStoreService.SearchAsync(queryEmbedding, topK: _retrievalTopK, ct);
            var matches = FilterMatches(rawMatches);

            logger.LogInformation(
                "RAG retrieval completed. TopK={TopK}, Threshold={Threshold}, RawCount={RawCount}, FilteredCount={FilteredCount}, Scores={Scores}",
                _retrievalTopK,
                _minSimilarityScore,
                rawMatches.Count,
                matches.Count,
                string.Join(",", rawMatches.Select(m => m.Score.ToString("F3"))));

            if (rawMatches.Count == 0)
            {
                logger.LogWarning(
                    "Vector store returned no candidates. ConversationId={ConversationId}, Model={Model}",
                    request.ConversationId,
                    _chatModel);
            }

            if (matches.Count == 0)
            {
                logger.LogWarning(
                    "No relevant SOP context found above threshold {Threshold} for conversation {ConversationId}.",
                    _minSimilarityScore,
                    request.ConversationId);
                return BuildNotFoundResponse(request);
            }

            // 2. Prepare Chat Messages
            var messages = BuildChatMessages(request, matches);

            // 3. Define Tools (server-controlled)
            var options = new ChatCompletionOptions();
            if (_enableTools)
            {
                foreach (var tool in BuildToolDefinitions()) options.Tools.Add(tool);
            }
            else
            {
                logger.LogInformation(
                    "Tool calling is disabled by server configuration. ConversationId={ConversationId}",
                    request.ConversationId);
            }

            var client = openAiClient.GetChatClient(_chatModel);
            var completionStopwatch = Stopwatch.StartNew();
            var response = await client.CompleteChatAsync(messages, options, ct);
            completionStopwatch.Stop();
            var chatCompletion = response.Value;

            // 4. Handle Tool Calls (Single Turn)
            if (_enableTools && chatCompletion.FinishReason == ChatFinishReason.ToolCalls)
            {
                matches = await HandleToolCallsAsync(chatCompletion, messages, matches, request.ConversationId, ct);
                completionStopwatch.Restart();
                response = await client.CompleteChatAsync(messages, options, ct);
                completionStopwatch.Stop();
                chatCompletion = response.Value;
            }

            totalStopwatch.Stop();
            logger.LogInformation(
                "Chat response generated. ConversationId={ConversationId}, Model={Model}, Mode={Mode}, ToolingEnabled={ToolingEnabled}, RetrievedChunkIds={ChunkIds}, RetrievedScores={Scores}, CompletionLatencyMs={CompletionLatencyMs}, TotalLatencyMs={TotalLatencyMs}",
                request.ConversationId,
                _chatModel,
                "openai",
                _enableTools,
                string.Join(",", matches.Select(m => m.Record.Id)),
                string.Join(",", matches.Select(m => m.Score.ToString("F3"))),
                completionStopwatch.ElapsedMilliseconds,
                totalStopwatch.ElapsedMilliseconds);

            return BuildChatResponse(request, chatCompletion, matches);
        }, cancellationToken);
    }

    private IReadOnlyList<VectorSearchMatch> FilterMatches(IReadOnlyList<VectorSearchMatch> matches) =>
        matches.Where(m => m.Score >= _minSimilarityScore).ToList();

    private static List<ChatMessage> BuildChatMessages(ChatRequest request, IEnumerable<VectorSearchMatch> matches)
    {
        var contextText = string.Join("\n\n", matches.Select(m => $"Source: {m.Record.Source}\nContent: {m.Record.ChunkText}"));
        var messages = new List<ChatMessage>
        {
            ChatMessage.CreateSystemMessage($"""
                You are a helpful assistant for a grocery store chain. 
                Answer the user's questions based ONLY on the provided SOP context.
                If you don't know the answer, say you don't know based on the SOP.
                Always provide helpful and concise answers.

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
        CancellationToken ct)
    {
        messages.Add(ChatMessage.CreateAssistantMessage(chatCompletion));
        var allMatches = matches.ToList();

        foreach (var toolCall in chatCompletion.ToolCalls)
        {
            await HandleSearchSopToolCallAsync(toolCall, messages, allMatches, conversationId, ct);
        }

        return allMatches;
    }

    private async Task HandleSearchSopToolCallAsync(
        ChatToolCall toolCall,
        List<ChatMessage> messages,
        List<VectorSearchMatch> allMatches,
        string conversationId,
        CancellationToken ct)
    {
        var queryParseResult = ToolCallingPolicy.TryExtractSearchQuery(toolCall.FunctionArguments.ToString(), out var query);
        if (queryParseResult == ToolCallQueryParseResult.InvalidJson)
        {
            logger.LogWarning(
                "Tool call parse failed. ConversationId={ConversationId}, ToolCallId={ToolCallId}, ToolName={ToolName}, Reason=InvalidJson",
                conversationId,
                toolCall.Id,
                toolCall.FunctionName);
            messages.Add(ChatMessage.CreateToolMessage(toolCall.Id, ""));
            return;
        }

        if (queryParseResult == ToolCallQueryParseResult.EmptyQuery)
        {
            logger.LogWarning(
                "Tool call rejected. ConversationId={ConversationId}, ToolCallId={ToolCallId}, ToolName={ToolName}, Reason=EmptyQuery",
                conversationId,
                toolCall.Id,
                toolCall.FunctionName);
            messages.Add(ChatMessage.CreateToolMessage(toolCall.Id, ""));
            return;
        }

        var toolQueryEmbedding = await embeddingService.EmbedAsync(query, ct);
        var rawToolMatches = await vectorStoreService.SearchAsync(toolQueryEmbedding, topK: _retrievalTopK, ct);
        var toolMatches = FilterMatches(rawToolMatches);

        logger.LogInformation(
            "Tool retrieval completed. TopK={TopK}, Threshold={Threshold}, RawCount={RawCount}, FilteredCount={FilteredCount}, Scores={Scores}",
            _retrievalTopK,
            _minSimilarityScore,
            rawToolMatches.Count,
            toolMatches.Count,
            string.Join(",", rawToolMatches.Select(m => m.Score.ToString("F3"))));

        if (toolMatches.Count == 0)
        {
            logger.LogWarning(
                "Tool retrieval returned no relevant matches. ConversationId={ConversationId}, ToolCallId={ToolCallId}, ToolName={ToolName}, Query={Query}, Threshold={Threshold}",
                conversationId,
                toolCall.Id,
                toolCall.FunctionName,
                query,
                _minSimilarityScore);
            messages.Add(ChatMessage.CreateToolMessage(toolCall.Id, string.Empty));
            return;
        }

        var toolContext = ToolCallingPolicy.BuildToolContext(toolMatches);
        messages.Add(ChatMessage.CreateToolMessage(toolCall.Id, toolContext));

        allMatches.AddRange(toolMatches.Where(m => allMatches.All(existing => existing.Record.Id != m.Record.Id)));
    }

    private static ChatResponse BuildNotFoundResponse(ChatRequest request) =>
        new()
        {
            ConversationId = request.ConversationId,
            Status = "success",
            IsPlaceholder = false,
            AssistantMessage = NoRelevantContextMessage,
            ToolCalls = [],
            Citations = []
        };

    private static ChatResponse BuildChatResponse(ChatRequest request, ChatCompletion chatCompletion, IEnumerable<VectorSearchMatch> matches) =>
        new()
        {
            ConversationId = request.ConversationId,
            Status = "success",
            IsPlaceholder = false,
            AssistantMessage = chatCompletion.Content.Count > 0 ? chatCompletion.Content[0].Text : string.Empty,
            Citations = matches.Select(CitationMapper.FromMatch).ToList()
        };
}

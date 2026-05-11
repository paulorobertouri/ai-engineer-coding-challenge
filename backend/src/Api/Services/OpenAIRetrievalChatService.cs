using Api.Contracts;
using Api.Models;
using Api.Services;
using OpenAI;
using OpenAI.Chat;
using Polly;
using Polly.Retry;
using System.Text.Json;

namespace Api.Services;

public sealed class OpenAIRetrievalChatService(
    OpenAIClient openAiClient,
    IConfiguration configuration,
    IEmbeddingService embeddingService,
    IVectorStoreService vectorStoreService,
    ILogger<OpenAIRetrievalChatService> logger) : IRetrievalChatService
{
    private readonly string _chatModel = configuration["OpenAI:ChatModel"] ?? "gpt-4o-mini";
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

    public async Task<ChatResponse> GenerateResponseAsync(ChatRequest request, CancellationToken cancellationToken = default)
    {
        return await _resiliencePipeline.ExecuteAsync(async ct =>
        {
            var latestUserMessage = request.Messages
                .LastOrDefault(m => m.Role.Equals("user", StringComparison.OrdinalIgnoreCase))?.Content ?? "";

            // 1. Initial Retrieval (Standard RAG)
            var queryEmbedding = await embeddingService.EmbedAsync(latestUserMessage, ct);
            var matches = await vectorStoreService.SearchAsync(queryEmbedding, topK: 3, ct);

            // 2. Prepare Chat Messages
            var messages = BuildChatMessages(request, matches);

            // 3. Define Tools (only when the caller opted in)
            var options = new ChatCompletionOptions();
            if (request.UseTools)
            {
                foreach (var tool in BuildToolDefinitions()) options.Tools.Add(tool);
            }

            var client = openAiClient.GetChatClient(_chatModel);
            var response = await client.CompleteChatAsync(messages, options, ct);
            var chatCompletion = response.Value;

            // 4. Handle Tool Calls (Single Turn)
            if (chatCompletion.FinishReason == ChatFinishReason.ToolCalls)
            {
                matches = await HandleToolCallsAsync(chatCompletion, messages, matches, ct);
                response = await client.CompleteChatAsync(messages, options, ct);
                chatCompletion = response.Value;
            }

            return BuildChatResponse(request, chatCompletion, matches);
        }, cancellationToken);
    }

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
        CancellationToken ct)
    {
        messages.Add(ChatMessage.CreateAssistantMessage(chatCompletion));
        var allMatches = matches.ToList();

        foreach (var toolCall in chatCompletion.ToolCalls)
        {
            await HandleSearchSopToolCallAsync(toolCall, messages, allMatches, ct);
        }

        return allMatches;
    }

    private async Task HandleSearchSopToolCallAsync(
        ChatToolCall toolCall,
        List<ChatMessage> messages,
        List<VectorSearchMatch> allMatches,
        CancellationToken ct)
    {
        string query;
        try
        {
            var args = JsonDocument.Parse(toolCall.FunctionArguments).RootElement;
            query = args.TryGetProperty("query", out var queryProp) ? queryProp.GetString() ?? "" : "";
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Failed to parse search_sop tool arguments; skipping tool call.");
            messages.Add(ChatMessage.CreateToolMessage(toolCall.Id, ""));
            return;
        }

        if (string.IsNullOrWhiteSpace(query))
        {
            logger.LogWarning("Received empty search_sop query; skipping tool call.");
            messages.Add(ChatMessage.CreateToolMessage(toolCall.Id, ""));
            return;
        }

        var toolQueryEmbedding = await embeddingService.EmbedAsync(query, ct);
        var toolMatches = await vectorStoreService.SearchAsync(toolQueryEmbedding, topK: 3, ct);
        var toolContext = string.Join("\n\n", toolMatches.Select(m => m.Record.ChunkText));
        messages.Add(ChatMessage.CreateToolMessage(toolCall.Id, toolContext));

        allMatches.AddRange(toolMatches.Where(m => allMatches.All(existing => existing.Record.Id != m.Record.Id)));
    }

    private static ChatResponse BuildChatResponse(ChatRequest request, ChatCompletion chatCompletion, IEnumerable<VectorSearchMatch> matches) =>
        new()
        {
            ConversationId = request.ConversationId,
            Status = "success",
            IsPlaceholder = false,
            AssistantMessage = chatCompletion.Content.Count > 0 ? chatCompletion.Content[0].Text : string.Empty,
            Citations = matches.Select(m => new CitationDto
            {
                Source = m.Record.Source,
                Snippet = m.Record.ChunkText.Length > 200 ? m.Record.ChunkText[..200] + "..." : m.Record.ChunkText
            }).ToList()
        };
}

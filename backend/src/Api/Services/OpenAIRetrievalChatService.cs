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
    private readonly string _chatModel = configuration["OpenAI:ChatModel"] ?? "gpt-5-mini";
    private readonly ResiliencePipeline _resiliencePipeline = new ResiliencePipelineBuilder()
        .AddRetry(new RetryStrategyOptions
        {
            ShouldHandle = new PredicateBuilder().Handle<Exception>(),
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

    public async Task<ChatResponse> GenerateResponseAsync(ChatRequest request, CancellationToken cancellationToken = default)
    {
        return await _resiliencePipeline.ExecuteAsync(async ct =>
        {
            var latestUserMessage = request.Messages.LastOrDefault(m => m.Role.Equals("user", StringComparison.OrdinalIgnoreCase))?.Content ?? "";

            // 1. Initial Retrieval (Standard RAG)
            var queryEmbedding = await embeddingService.EmbedAsync(latestUserMessage, ct);
            var matches = await vectorStoreService.SearchAsync(queryEmbedding, topK: 3, ct);

            var contextText = string.Join("\n\n", matches.Select(m => $"Source: {m.Record.Source}\nContent: {m.Record.ChunkText}"));

            // 2. Prepare Chat Messages
            var messages = new List<ChatMessage>();
            messages.Add(ChatMessage.CreateSystemMessage($"""
                You are a helpful assistant for a grocery store chain. 
                Answer the user's questions based ONLY on the provided SOP context.
                If you don't know the answer, say you don't know based on the SOP.
                Always provide helpful and concise answers.

                CONTEXT:
                {contextText}
                """));

            foreach (var msg in request.Messages)
            {
                if (msg.Role.Equals("user", StringComparison.OrdinalIgnoreCase))
                    messages.Add(ChatMessage.CreateUserMessage(msg.Content));
                else if (msg.Role.Equals("assistant", StringComparison.OrdinalIgnoreCase))
                    messages.Add(ChatMessage.CreateAssistantMessage(msg.Content));
            }

            // 3. Define Tools
            var tools = new List<ChatTool>
            {
                ChatTool.CreateFunctionTool(
                    "get_store_hours",
                    "Retrieves the standard operating hours for the grocery store."
                ),
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
                        required = new[] { "query" }
                    })
                )
            };

            var options = new ChatCompletionOptions();
            foreach (var tool in tools) options.Tools.Add(tool);

            var client = openAiClient.GetChatClient(_chatModel);
            var response = await client.CompleteChatAsync(messages, options, ct);

            var chatCompletion = response.Value;

            // 4. Handle Tool Calls (Single Turn)
            if (chatCompletion.FinishReason == ChatFinishReason.ToolCalls)
            {
                messages.Add(ChatMessage.CreateAssistantMessage(chatCompletion));

                foreach (var toolCall in chatCompletion.ToolCalls)
                {
                    if (toolCall.FunctionName == "get_store_hours")
                    {
                        var hours = """
                            Monday – Friday: 6:00 AM – 11:00 PM
                            Saturday: 7:00 AM – 11:00 PM
                            Sunday: 7:00 AM – 10:00 PM
                            """;
                        messages.Add(ChatMessage.CreateToolMessage(toolCall.Id, hours));
                    }
                    else if (toolCall.FunctionName == "search_sop")
                    {
                        var args = JsonDocument.Parse(toolCall.FunctionArguments).RootElement;
                        var query = args.GetProperty("query").GetString() ?? "";
                        var toolQueryEmbedding = await embeddingService.EmbedAsync(query, ct);
                        var toolMatches = await vectorStoreService.SearchAsync(toolQueryEmbedding, topK: 3, ct);
                        var toolContext = string.Join("\n\n", toolMatches.Select(m => m.Record.ChunkText));
                        messages.Add(ChatMessage.CreateToolMessage(toolCall.Id, toolContext));

                        matches = matches.Concat(toolMatches).DistinctBy(m => m.Record.Id).ToList();
                    }
                }

                response = await client.CompleteChatAsync(messages, options, ct);
                chatCompletion = response.Value;
            }

            return new ChatResponse
            {
                ConversationId = request.ConversationId,
                Status = "success",
                IsPlaceholder = false,
                AssistantMessage = chatCompletion.Content[0].Text,
                Citations = matches.Select(m => new CitationDto
                {
                    Source = m.Record.Source,
                    Snippet = m.Record.ChunkText.Length > 200 ? m.Record.ChunkText[..200] + "..." : m.Record.ChunkText
                }).ToList()
            };
        }, cancellationToken);
    }
}
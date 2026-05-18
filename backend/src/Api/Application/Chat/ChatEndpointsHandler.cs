using Api.Application.Feedback;
using Api.Contracts;
using Api.Options;
using Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace Api.Application.Chat;

public sealed class ChatEndpointsHandler(
    IRetrievalChatService retrievalChatService,
    IConversationFeedbackService conversationFeedbackService,
    IOptions<TimeoutOptions> timeoutOptions)
{
    private const string ValidationErrorTitle = "One or more validation errors occurred.";
    private const int StreamChunkSize = 24;
    private static readonly JsonSerializerOptions SseJsonOptions = new(JsonSerializerDefaults.Web);
    private readonly SendChatMessageHandler _sendChatMessageHandler = new(retrievalChatService, timeoutOptions);
    private readonly SubmitConversationFeedbackHandler _submitConversationFeedbackHandler = new(conversationFeedbackService);

    public async Task<ActionResult<ChatResponse>> Post(
        ChatRequest request,
        CancellationToken cancellationToken,
        string? requestPath = null)
    {
        var validationResult = ValidateRequest(request);
        if (validationResult is not null)
        {
            return validationResult;
        }

        try
        {
            var response = await _sendChatMessageHandler.HandleAsync(new SendChatMessageCommand(request), cancellationToken);
            return new OkObjectResult(response);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            var details = ApiErrorFactory.RequestTimeout(
                "Chat request timed out.",
                "The chat request took too long to complete. Please retry.",
                requestPath);
            return new ObjectResult(details) { StatusCode = StatusCodes.Status408RequestTimeout };
        }
    }

    public async Task<IActionResult> Stream(
        ChatRequest request,
        HttpResponse response,
        CancellationToken cancellationToken,
        string? requestPath = null)
    {
        var validationResult = ValidateRequest(request);
        if (validationResult is not null)
        {
            return validationResult.Result ?? new ObjectResult(validationResult.Value);
        }

        try
        {
            var chatResponse = await _sendChatMessageHandler.HandleAsync(new SendChatMessageCommand(request), cancellationToken);

            response.StatusCode = StatusCodes.Status200OK;
            response.ContentType = "text/event-stream";
            response.Headers.CacheControl = "no-cache";
            response.Headers.Append("X-Accel-Buffering", "no");

            foreach (var chunk in SplitIntoStreamingChunks(chatResponse.AssistantMessage))
            {
                await WriteSseEventAsync(response, "delta", new { delta = chunk }, cancellationToken);
            }

            await WriteSseEventAsync(response, "complete", chatResponse, cancellationToken);
            return new EmptyResult();
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            var details = ApiErrorFactory.RequestTimeout(
                "Chat request timed out.",
                "The chat request took too long to complete. Please retry.",
                requestPath);
            return new ObjectResult(details) { StatusCode = StatusCodes.Status408RequestTimeout };
        }
    }

    public async Task<ActionResult<ConversationFeedbackResponse>> SubmitFeedback(
        ConversationFeedbackRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _submitConversationFeedbackHandler.HandleAsync(
            new SubmitConversationFeedbackCommand(
                request.ConversationId,
                request.MessageId,
                request.FeedbackType,
                request.Comment),
            cancellationToken);

        return new OkObjectResult(response);
    }

    private ActionResult<ChatResponse>? ValidateRequest(ChatRequest request)
    {
        if (request.Messages.Count == 0)
            return ValidationError(nameof(ChatRequest.Messages), "At least one chat message is required.");

        if (request.Messages.Count > ChatRequest.MaxMessages)
            return ValidationError(nameof(ChatRequest.Messages), $"Chat requests are limited to {ChatRequest.MaxMessages} messages.");

        if (request.ConversationId.Length > ChatRequest.MaxConversationIdLength)
            return ValidationError(nameof(ChatRequest.ConversationId), $"ConversationId must not exceed {ChatRequest.MaxConversationIdLength} characters.");

        if (request.Messages.Any(m => m.Content.Length > ChatRequest.MaxMessageContentLength))
            return ValidationError(nameof(ChatRequest.Messages), $"Message content must not exceed {ChatRequest.MaxMessageContentLength} characters.");

        if (!string.IsNullOrWhiteSpace(request.UserRole)
            && !string.Equals(request.UserRole, "cashier", StringComparison.Ordinal)
            && !string.Equals(request.UserRole, "manager", StringComparison.Ordinal)
            && !string.Equals(request.UserRole, "department_lead", StringComparison.Ordinal))
        {
            return ValidationError(nameof(ChatRequest.UserRole), "UserRole must be cashier, manager, or department_lead.");
        }

        if (!ChatRequest.IsValidResponseLanguage(request.ResponseLanguage))
            return ValidationError(
                nameof(ChatRequest.ResponseLanguage),
                "ResponseLanguage must be a language tag like 'en', 'es', or 'pt-BR'.");

        if (!string.IsNullOrWhiteSpace(request.ResponseTone)
            && !string.Equals(request.ResponseTone, "neutral", StringComparison.Ordinal)
            && !string.Equals(request.ResponseTone, "formal", StringComparison.Ordinal)
            && !string.Equals(request.ResponseTone, "friendly", StringComparison.Ordinal))
        {
            return ValidationError(nameof(ChatRequest.ResponseTone), "ResponseTone must be neutral, formal, or friendly.");
        }

        if (!string.IsNullOrWhiteSpace(request.ResponseLength)
            && !string.Equals(request.ResponseLength, "short", StringComparison.Ordinal)
            && !string.Equals(request.ResponseLength, "medium", StringComparison.Ordinal)
            && !string.Equals(request.ResponseLength, "long", StringComparison.Ordinal))
        {
            return ValidationError(nameof(ChatRequest.ResponseLength), "ResponseLength must be short, medium, or long.");
        }

        if (!string.IsNullOrWhiteSpace(request.ResponseFormat)
            && !string.Equals(request.ResponseFormat, "paragraph", StringComparison.Ordinal)
            && !string.Equals(request.ResponseFormat, "bullets", StringComparison.Ordinal)
            && !string.Equals(request.ResponseFormat, "checklist", StringComparison.Ordinal))
        {
            return ValidationError(nameof(ChatRequest.ResponseFormat), "ResponseFormat must be paragraph, bullets, or checklist.");
        }

        if (!request.Messages.Any(m => string.Equals(m.Role, "user", StringComparison.OrdinalIgnoreCase)))
            return ValidationError(nameof(ChatRequest.Messages), "At least one user message is required.");

        return null;
    }

    private ActionResult<ChatResponse> ValidationError(string field, string message)
    {
        var details = ApiErrorFactory.Validation(field, message, ValidationErrorTitle);
        return new BadRequestObjectResult(details);
    }

    private static async Task WriteSseEventAsync(HttpResponse response, string eventName, object payload, CancellationToken cancellationToken)
    {
        await response.WriteAsync($"event: {eventName}\n", cancellationToken);
        await response.WriteAsync($"data: {JsonSerializer.Serialize(payload, SseJsonOptions)}\n\n", cancellationToken);
        await response.Body.FlushAsync(cancellationToken);
    }

    private static IEnumerable<string> SplitIntoStreamingChunks(string content)
    {
        if (string.IsNullOrEmpty(content))
        {
            yield break;
        }

        for (var i = 0; i < content.Length; i += StreamChunkSize)
        {
            var length = Math.Min(StreamChunkSize, content.Length - i);
            yield return content.Substring(i, length);
        }
    }
}
using Api.Contracts;
using Api.Options;
using Api.Services;
using Api.Security;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[Authorize(Policy = AuthorizationPolicies.ChatUser)]
public sealed class ChatController(
    IRetrievalChatService retrievalChatService,
    IOptions<TimeoutOptions> timeoutOptions) : ControllerBase
{
    private const string ValidationErrorTitle = "One or more validation errors occurred.";
    private const int StreamChunkSize = 24;
    private static readonly JsonSerializerOptions SseJsonOptions = new(JsonSerializerDefaults.Web);

    [HttpPost]
    [EnableRateLimiting("chat")]
    public async Task<ActionResult<ChatResponse>> Post([FromBody] ChatRequest request, CancellationToken cancellationToken)
    {
        var validationResult = ValidateRequest(request);
        if (validationResult is not null)
        {
            return validationResult;
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(timeoutOptions.Value.ChatSeconds));

        try
        {
            var response = await retrievalChatService.GenerateResponseAsync(request, timeoutCts.Token);
            return Ok(response);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            var details = ApiErrorFactory.RequestTimeout(
                "Chat request timed out.",
                "The chat request took too long to complete. Please retry.",
                HttpContext?.Request.Path.Value);
            return StatusCode(StatusCodes.Status408RequestTimeout, details);
        }
    }

    [HttpPost("stream")]
    [EnableRateLimiting("chat")]
    public async Task<IActionResult> Stream([FromBody] ChatRequest request, CancellationToken cancellationToken)
    {
        var validationResult = ValidateRequest(request);
        if (validationResult is not null)
        {
            return validationResult.Result ?? new ObjectResult(validationResult.Value);
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(timeoutOptions.Value.ChatSeconds));

        try
        {
            var response = await retrievalChatService.GenerateResponseAsync(request, timeoutCts.Token);

            Response.StatusCode = StatusCodes.Status200OK;
            Response.ContentType = "text/event-stream";
            Response.Headers.CacheControl = "no-cache";
            Response.Headers.Append("X-Accel-Buffering", "no");

            foreach (var chunk in SplitIntoStreamingChunks(response.AssistantMessage))
            {
                await WriteSseEventAsync("delta", new { delta = chunk }, timeoutCts.Token);
            }

            await WriteSseEventAsync("complete", response, timeoutCts.Token);
            return new EmptyResult();
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            var details = ApiErrorFactory.RequestTimeout(
                "Chat request timed out.",
                "The chat request took too long to complete. Please retry.",
                HttpContext?.Request.Path.Value);
            return StatusCode(StatusCodes.Status408RequestTimeout, details);
        }
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

        if (!request.Messages.Any(m => string.Equals(m.Role, "user", StringComparison.OrdinalIgnoreCase)))
            return ValidationError(nameof(ChatRequest.Messages), "At least one user message is required.");

        return null;
    }

    private ActionResult<ChatResponse> ValidationError(string field, string message)
    {
        var details = ApiErrorFactory.Validation(field, message, ValidationErrorTitle);

        return ValidationProblem(details);
    }

    private async Task WriteSseEventAsync(string eventName, object payload, CancellationToken cancellationToken)
    {
        await Response.WriteAsync($"event: {eventName}\n", cancellationToken);
        await Response.WriteAsync($"data: {JsonSerializer.Serialize(payload, SseJsonOptions)}\n\n", cancellationToken);
        await Response.Body.FlushAsync(cancellationToken);
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
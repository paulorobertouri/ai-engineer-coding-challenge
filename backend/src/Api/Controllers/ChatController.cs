using Api.Contracts;
using Api.Services;
using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
public sealed class ChatController(IRetrievalChatService retrievalChatService) : ControllerBase
{
    private const string ValidationErrorTitle = "One or more validation errors occurred.";

    [HttpPost]
    [EnableRateLimiting("chat")]
    public async Task<ActionResult<ChatResponse>> Post([FromBody] ChatRequest request, CancellationToken cancellationToken)
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

        var response = await retrievalChatService.GenerateResponseAsync(request, cancellationToken);
        return Ok(response);
    }

    private ActionResult<ChatResponse> ValidationError(string field, string message)
    {
        var details = new ValidationProblemDetails(new Dictionary<string, string[]>
        {
            [field] = [message]
        })
        {
            Status = StatusCodes.Status400BadRequest,
            Title = ValidationErrorTitle
        };

        return ValidationProblem(details);
    }
}
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
    [HttpPost]
    [EnableRateLimiting("chat")]
    public async Task<ActionResult<ChatResponse>> Post([FromBody] ChatRequest request, CancellationToken cancellationToken)
    {
        if (request.Messages.Count == 0)
        {
            return BadRequest(new { error = "At least one chat message is required." });
        }

        var response = await retrievalChatService.GenerateResponseAsync(request, cancellationToken);
        return Ok(response);
    }
}
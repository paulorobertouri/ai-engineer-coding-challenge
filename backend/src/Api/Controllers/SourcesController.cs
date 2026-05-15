using Api.Contracts;
using Api.Security;
using Api.Services;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[Authorize(Policy = AuthorizationPolicies.ChatUser)]
public sealed class SourcesController(ISourceDocumentViewerService sourceDocumentViewerService) : ControllerBase
{
    [HttpGet("document")]
    public async Task<ActionResult<SourceDocumentResponse>> GetDocument(
        [FromQuery] string source,
        [FromQuery] string? knowledgeBaseId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return BadRequest(ApiErrorFactory.BadRequest("Source is required.", "Query parameter 'source' is required."));
        }

        var document = await sourceDocumentViewerService.GetDocumentAsync(source, knowledgeBaseId, cancellationToken);
        if (document is null)
        {
            return NotFound(ApiErrorFactory.NotFound(
                "Source document not found.",
                $"No ingested chunks were found for source '{Path.GetFileName(source)}'."));
        }

        return Ok(document);
    }
}

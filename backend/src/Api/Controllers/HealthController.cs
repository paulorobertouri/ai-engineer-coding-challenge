using Api.Contracts;
using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
public sealed class HealthController(IConfiguration configuration) : ControllerBase
{
    [HttpGet]
    public ActionResult<HealthResponse> Get()
    {
        var hasApiKey = !string.IsNullOrWhiteSpace(configuration["OpenAI:ApiKey"]);

        var notes = hasApiKey
            ? new List<string>
            {
                "Service is fully operational.",
                "RAG Ingestion, JSON Vector Store, and Tool-calling are active.",
                "Resilience pipelines and strict validation are enabled."
            }
            : new List<string>
            {
                "Service is running in offline/fallback mode (no OpenAI API key).",
                "Using deterministic embeddings and keyword-based chat responses.",
                "Set OpenAI__ApiKey to enable full AI capabilities."
            };

        return Ok(new HealthResponse
        {
            Status = "ok",
            Service = "grocery-store-sop-assistant-api",
            UtcTime = DateTimeOffset.UtcNow,
            Notes = notes
        });
    }
}
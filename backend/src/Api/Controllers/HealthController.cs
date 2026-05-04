using Api.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class HealthController : ControllerBase
{
    [HttpGet]
    public ActionResult<HealthResponse> Get()
    {
        return Ok(new HealthResponse
        {
            Status = "ok",
            Service = "grocery-store-sop-assistant-api",
            UtcTime = DateTimeOffset.UtcNow,
            Notes =
            [
                "Service is fully operational.",
                "RAG Ingestion, JSON Vector Store, and Tool-calling are active.",
                "Resilience pipelines and strict validation are enabled."
            ]
        });
    }
}
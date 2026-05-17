using Api.Application.Health;
using Api.Contracts;
using Api.Models;
using Api.Options;
using Asp.Versioning;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
public sealed class HealthController(
    IOptions<OpenAIOptions> openAiOptions,
    IOptions<ChallengeOptions> challengeOptions,
    IOptions<VectorStoreOptions> vectorStoreOptions,
    IOptions<HealthChecksOptions> healthChecksOptions,
    IWebHostEnvironment environment) : ControllerBase
{
    private readonly GetHealthQueryHandler _getHealthQueryHandler = new(openAiOptions);
    private readonly GetReadinessQueryHandler _getReadinessQueryHandler = new(openAiOptions, challengeOptions, vectorStoreOptions, healthChecksOptions, environment);

    [HttpGet]
    public async Task<ActionResult<HealthResponse>> Get(CancellationToken cancellationToken)
    {
        var response = await _getHealthQueryHandler.HandleAsync(new GetHealthQuery(), cancellationToken);
        return Ok(response);
    }

    [HttpGet("/api/v{version:apiVersion}/ready")]
    public async Task<ActionResult<HealthResponse>> Ready(CancellationToken cancellationToken)
    {
        var response = await _getReadinessQueryHandler.HandleAsync(new GetReadinessQuery(), cancellationToken);

        return string.Equals(response.Status, "ready", StringComparison.Ordinal)
            ? Ok(response)
            : StatusCode(StatusCodes.Status503ServiceUnavailable, response);
    }
}
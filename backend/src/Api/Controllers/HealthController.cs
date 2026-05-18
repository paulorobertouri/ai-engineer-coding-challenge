using Api.Application.Health;
using Api.Contracts;
using Api.Models;
using Api.Options;
using Api.Services;
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
    IVectorStoreService vectorStoreService,
    IOptions<HealthChecksOptions> healthChecksOptions,
    IEndpointSloTracker endpointSloTracker,
    IWebHostEnvironment environment) : ControllerBase
{
    private const int MaxReadinessTransitions = 20;
    private static readonly object ReadinessHistorySync = new();
    private static readonly Queue<ReadinessHistoryEntryDto> ReadinessHistory = new();
    private static string? _lastReadinessStatus;

    private readonly GetHealthQueryHandler _getHealthQueryHandler = new(openAiOptions, vectorStoreService);
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
        TrackReadinessTransition(response.Status, response.Notes);

        return string.Equals(response.Status, "ready", StringComparison.Ordinal)
            ? Ok(response)
            : StatusCode(StatusCodes.Status503ServiceUnavailable, response);
    }

    [HttpGet("/api/v{version:apiVersion}/ready/history")]
    public ActionResult<IReadOnlyList<ReadinessHistoryEntryDto>> ReadyHistory()
    {
        lock (ReadinessHistorySync)
        {
            return Ok(ReadinessHistory.ToList());
        }
    }

    [HttpGet("/api/v{version:apiVersion}/health/slo")]
    public ActionResult<EndpointSloSummaryResponse> SloSummary([FromQuery] int maxEndpoints = 20)
    {
        return Ok(endpointSloTracker.BuildSummary(maxEndpoints));
    }

    private static void TrackReadinessTransition(string currentStatus, IReadOnlyList<string> notes)
    {
        lock (ReadinessHistorySync)
        {
            if (string.Equals(_lastReadinessStatus, currentStatus, StringComparison.Ordinal))
            {
                return;
            }

            ReadinessHistory.Enqueue(new ReadinessHistoryEntryDto
            {
                PreviousStatus = _lastReadinessStatus,
                CurrentStatus = currentStatus,
                ChangedAtUtc = DateTimeOffset.UtcNow,
                Notes = notes.ToList()
            });

            while (ReadinessHistory.Count > MaxReadinessTransitions)
            {
                ReadinessHistory.Dequeue();
            }

            _lastReadinessStatus = currentStatus;
        }
    }
}
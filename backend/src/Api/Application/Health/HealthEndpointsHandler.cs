using Api.Contracts;
using Api.Models;
using Api.Options;
using Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Api.Application.Health;

public sealed class HealthEndpointsHandler(
    IOptions<OpenAIOptions> openAiOptions,
    IOptions<ChallengeOptions> challengeOptions,
    IOptions<VectorStoreOptions> vectorStoreOptions,
    IVectorStoreService vectorStoreService,
    IOptions<HealthChecksOptions> healthChecksOptions,
    IEndpointSloTracker endpointSloTracker,
    IWebHostEnvironment environment)
{
    private const int MaxReadinessTransitions = 20;
    private static readonly object ReadinessHistorySync = new();
    private static readonly Queue<ReadinessHistoryEntryDto> ReadinessHistory = new();
    private static string? _lastReadinessStatus;

    private readonly GetHealthQueryHandler _getHealthQueryHandler = new(openAiOptions, vectorStoreService);
    private readonly GetReadinessQueryHandler _getReadinessQueryHandler = new(openAiOptions, challengeOptions, vectorStoreOptions, healthChecksOptions, environment);

    public async Task<ActionResult<HealthResponse>> Get(CancellationToken cancellationToken)
    {
        var response = await _getHealthQueryHandler.HandleAsync(new GetHealthQuery(), cancellationToken);
        return new OkObjectResult(response);
    }

    public async Task<ActionResult<HealthResponse>> Ready(CancellationToken cancellationToken)
    {
        var response = await _getReadinessQueryHandler.HandleAsync(new GetReadinessQuery(), cancellationToken);
        TrackReadinessTransition(response.Status, response.Notes);

        return string.Equals(response.Status, "ready", StringComparison.Ordinal)
            ? new OkObjectResult(response)
            : new ObjectResult(response) { StatusCode = StatusCodes.Status503ServiceUnavailable };
    }

    public ActionResult<IReadOnlyList<ReadinessHistoryEntryDto>> ReadyHistory()
    {
        lock (ReadinessHistorySync)
        {
            return new OkObjectResult(ReadinessHistory.ToList());
        }
    }

    public ActionResult<EndpointSloSummaryResponse> SloSummary(int maxEndpoints = 20)
    {
        return new OkObjectResult(endpointSloTracker.BuildSummary(maxEndpoints));
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
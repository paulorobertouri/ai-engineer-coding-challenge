using Api.Contracts;
using Api.Services;
using Api.Security;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/Ingest/jobs")]
[EnableRateLimiting("ingest")]
public sealed class IngestJobsController(
    IIngestJobDispatcher ingestJobDispatcher,
    IIngestJobStatusStore ingestJobStatusStore) : ControllerBase
{
    [HttpGet("{jobId:guid}")]
    [Authorize(Policy = AuthorizationPolicies.KnowledgeAdmin)]
    public ActionResult<IngestJobStatusResponse> GetJobStatus(Guid jobId)
    {
        var job = ingestJobStatusStore.Get(jobId);
        if (job is null)
        {
            return NotFound(ApiErrorFactory.NotFound(
                "Ingest job not found.",
                $"No ingest job exists for '{jobId}'."));
        }

        return Ok(ToJobStatusResponse(job));
    }

    [HttpGet]
    [Authorize(Policy = AuthorizationPolicies.KnowledgeAdmin)]
    public ActionResult<IReadOnlyList<IngestJobStatusResponse>> ListJobs([FromQuery] int limit = 100)
    {
        var jobs = ingestJobStatusStore
            .List(limit)
            .Select(ToJobStatusResponse)
            .ToList();

        return Ok(jobs);
    }

    [HttpGet("dead-letter")]
    [Authorize(Policy = AuthorizationPolicies.KnowledgeAdmin)]
    public ActionResult<IReadOnlyList<IngestJobStatusResponse>> ListDeadLetterJobs([FromQuery] int limit = 100)
    {
        var jobs = ingestJobStatusStore
            .GetDeadLetters(limit)
            .Select(ToJobStatusResponse)
            .ToList();

        return Ok(jobs);
    }

    [HttpPost("{jobId:guid}/cancel")]
    [Authorize(Policy = AuthorizationPolicies.KnowledgeAdmin)]
    public ActionResult<IngestJobStatusResponse> CancelJob(Guid jobId)
    {
        if (!ingestJobStatusStore.TryCancel(jobId))
        {
            return Conflict(ApiErrorFactory.Conflict(
                "Ingest job cannot be canceled.",
                $"Ingest job '{jobId}' is not in queued state."));
        }

        var job = ingestJobStatusStore.Get(jobId);
        if (job is null)
        {
            return NotFound(ApiErrorFactory.NotFound(
                "Ingest job not found.",
                $"No ingest job exists for '{jobId}'."));
        }

        return Ok(ToJobStatusResponse(job));
    }

    [HttpPost("{jobId:guid}/retry")]
    [Authorize(Policy = AuthorizationPolicies.KnowledgeAdmin)]
    public async Task<ActionResult<IngestResponse>> RetryJob(Guid jobId, CancellationToken cancellationToken)
    {
        try
        {
            var submission = await ingestJobDispatcher.RetryFailedAsync(jobId, cancellationToken);
            return Ok(submission.Response);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ApiErrorFactory.Conflict("Ingest retry rejected.", ex.Message));
        }
    }

    [HttpPost("{jobId:guid}/priority")]
    [Authorize(Policy = AuthorizationPolicies.KnowledgeAdmin)]
    public ActionResult<IngestJobStatusResponse> UpdateJobPriority(Guid jobId, [FromBody] IngestJobPriorityUpdateRequest request)
    {
        if (!ingestJobStatusStore.TryUpdatePriority(jobId, request.Priority))
        {
            return Conflict(ApiErrorFactory.Conflict(
                "Ingest priority update rejected.",
                $"Ingest job '{jobId}' is not in queued state."));
        }

        var job = ingestJobStatusStore.Get(jobId);
        if (job is null)
        {
            return NotFound(ApiErrorFactory.NotFound(
                "Ingest job not found.",
                $"No ingest job exists for '{jobId}'."));
        }

        return Ok(ToJobStatusResponse(job));
    }

    private static IngestJobStatusResponse ToJobStatusResponse(IngestJobStatus job)
    {
        return new IngestJobStatusResponse
        {
            JobId = job.JobId,
            KnowledgeBaseId = job.KnowledgeBaseId,
            Status = job.State.ToString().ToLowerInvariant(),
            Message = job.State switch
            {
                IngestJobState.Queued => "Ingest job queued.",
                IngestJobState.Running => "Ingest job is running.",
                IngestJobState.Succeeded => job.Response?.Message ?? "Ingest job completed successfully.",
                IngestJobState.Failed => job.ErrorMessage ?? "Ingest job failed.",
                IngestJobState.Canceled => "Ingest job canceled.",
                _ => "Unknown job state."
            },
            QueuedAtUtc = job.QueuedAtUtc,
            StartedAtUtc = job.StartedAtUtc,
            CompletedAtUtc = job.CompletedAtUtc,
            Result = job.Response,
            ErrorMessage = job.ErrorMessage,
            Priority = job.Priority
        };
    }
}
using Api.Contracts;
using Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Api.Application.Ingest;

public sealed class IngestJobsEndpointsHandler(
    IIngestJobDispatcher ingestJobDispatcher,
    IIngestJobStatusStore ingestJobStatusStore)
{
    public ActionResult<IngestJobStatusResponse> GetJobStatus(Guid jobId)
    {
        var job = ingestJobStatusStore.Get(jobId);
        if (job is null)
        {
            return new NotFoundObjectResult(ApiErrorFactory.NotFound(
                "Ingest job not found.",
                $"No ingest job exists for '{jobId}'."));
        }

        return new OkObjectResult(ToJobStatusResponse(job));
    }

    public ActionResult<IReadOnlyList<IngestJobStatusResponse>> ListJobs(int limit = 100)
    {
        var jobs = ingestJobStatusStore
            .List(limit)
            .Select(ToJobStatusResponse)
            .ToList();

        return new OkObjectResult(jobs);
    }

    public ActionResult<IReadOnlyList<IngestJobStatusResponse>> ListDeadLetterJobs(int limit = 100)
    {
        var jobs = ingestJobStatusStore
            .GetDeadLetters(limit)
            .Select(ToJobStatusResponse)
            .ToList();

        return new OkObjectResult(jobs);
    }

    public ActionResult<IngestJobStatusResponse> CancelJob(Guid jobId)
    {
        if (!ingestJobStatusStore.TryCancel(jobId))
        {
            return new ConflictObjectResult(ApiErrorFactory.Conflict(
                "Ingest job cannot be canceled.",
                $"Ingest job '{jobId}' is not in queued state."));
        }

        var job = ingestJobStatusStore.Get(jobId);
        if (job is null)
        {
            return new NotFoundObjectResult(ApiErrorFactory.NotFound(
                "Ingest job not found.",
                $"No ingest job exists for '{jobId}'."));
        }

        return new OkObjectResult(ToJobStatusResponse(job));
    }

    public async Task<ActionResult<IngestResponse>> RetryJob(Guid jobId, CancellationToken cancellationToken)
    {
        try
        {
            var submission = await ingestJobDispatcher.RetryFailedAsync(jobId, cancellationToken);
            return new OkObjectResult(submission.Response);
        }
        catch (InvalidOperationException ex)
        {
            return new ConflictObjectResult(ApiErrorFactory.Conflict("Ingest retry rejected.", ex.Message));
        }
    }

    public ActionResult<IngestJobStatusResponse> UpdateJobPriority(Guid jobId, IngestJobPriorityUpdateRequest request)
    {
        if (!ingestJobStatusStore.TryUpdatePriority(jobId, request.Priority))
        {
            return new ConflictObjectResult(ApiErrorFactory.Conflict(
                "Ingest priority update rejected.",
                $"Ingest job '{jobId}' is not in queued state."));
        }

        var job = ingestJobStatusStore.Get(jobId);
        if (job is null)
        {
            return new NotFoundObjectResult(ApiErrorFactory.NotFound(
                "Ingest job not found.",
                $"No ingest job exists for '{jobId}'."));
        }

        return new OkObjectResult(ToJobStatusResponse(job));
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
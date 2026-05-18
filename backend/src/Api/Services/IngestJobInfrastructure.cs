using System.Collections.Concurrent;
using System.Threading.Channels;
using Api.Contracts;
using Api.Models;
using Api.Options;
using Microsoft.Extensions.Options;

namespace Api.Services;

public sealed record IngestJobRequest(
    Guid JobId,
    string Action,
    string KnowledgeBaseId,
    string SourceText,
    string SourceName,
    string DisplayPath,
    IReadOnlyList<VectorRecord> AllExistingRecords,
    IReadOnlyList<VectorRecord> ExistingRecords,
    string? PrecomputedSourceChecksum,
    bool ForceReingest,
    int Priority = 0,
    Guid? RetryOfJobId = null);

public sealed record IngestJobSubmission(
    Guid JobId,
    IngestResponse Response,
    bool IsBackground);

public enum IngestJobState
{
    Queued,
    Running,
    Succeeded,
    Failed,
    Canceled
}

public sealed class IngestJobStatus
{
    public Guid JobId { get; init; }

    public string KnowledgeBaseId { get; init; } = string.Empty;

    public IngestJobState State { get; set; }

    public DateTimeOffset QueuedAtUtc { get; init; }

    public DateTimeOffset? StartedAtUtc { get; set; }

    public DateTimeOffset? CompletedAtUtc { get; set; }

    public IngestResponse? Response { get; set; }

    public string? ErrorMessage { get; set; }

    public int Priority { get; set; }
}

public interface IIngestJobStatusStore
{
    bool HasActiveJob(string knowledgeBaseId);

    IngestJobStatus? Get(Guid jobId);

    IReadOnlyList<IngestJobStatus> List(int limit = 100);

    IReadOnlyList<IngestJobStatus> GetDeadLetters(int limit = 100);

    IngestJobRequest? GetRequestSnapshot(Guid jobId);

    void MarkQueued(Guid jobId, string knowledgeBaseId, IngestResponse response, IngestJobRequest request);

    void MarkRunning(Guid jobId);

    void MarkSucceeded(Guid jobId, IngestResponse response);

    void MarkFailed(Guid jobId, string errorMessage);

    bool TryCancel(Guid jobId);

    bool TryUpdatePriority(Guid jobId, int priority);

    void ClearActiveJob(string knowledgeBaseId, Guid jobId);
}

public sealed class InMemoryIngestJobStatusStore : IIngestJobStatusStore
{
    private readonly ConcurrentDictionary<Guid, IngestJobStatus> _jobs = new();
    private readonly ConcurrentDictionary<string, Guid> _activeJobsByKnowledgeBase = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<Guid, IngestJobRequest> _requests = new();

    public bool HasActiveJob(string knowledgeBaseId) => _activeJobsByKnowledgeBase.ContainsKey(knowledgeBaseId);

    public IngestJobStatus? Get(Guid jobId) => _jobs.TryGetValue(jobId, out var job) ? job : null;

    public IReadOnlyList<IngestJobStatus> List(int limit = 100)
    {
        var normalizedLimit = Math.Clamp(limit, 1, 500);
        return _jobs.Values
            .OrderByDescending(job => job.QueuedAtUtc)
            .Take(normalizedLimit)
            .ToList();
    }

    public IReadOnlyList<IngestJobStatus> GetDeadLetters(int limit = 100)
    {
        var normalizedLimit = Math.Clamp(limit, 1, 500);
        return _jobs.Values
            .Where(job => job.State == IngestJobState.Failed)
            .OrderByDescending(job => job.CompletedAtUtc ?? job.QueuedAtUtc)
            .Take(normalizedLimit)
            .ToList();
    }

    public IngestJobRequest? GetRequestSnapshot(Guid jobId)
    {
        return _requests.TryGetValue(jobId, out var request) ? request : null;
    }

    public void MarkQueued(Guid jobId, string knowledgeBaseId, IngestResponse response, IngestJobRequest request)
    {
        var job = new IngestJobStatus
        {
            JobId = jobId,
            KnowledgeBaseId = knowledgeBaseId,
            State = IngestJobState.Queued,
            QueuedAtUtc = DateTimeOffset.UtcNow,
            Response = response,
            Priority = request.Priority
        };

        _jobs[jobId] = job;
        _requests[jobId] = request;
        _activeJobsByKnowledgeBase[knowledgeBaseId] = jobId;
    }

    public void MarkRunning(Guid jobId)
    {
        if (_jobs.TryGetValue(jobId, out var job))
        {
            job.State = IngestJobState.Running;
            job.StartedAtUtc = DateTimeOffset.UtcNow;
        }
    }

    public void MarkSucceeded(Guid jobId, IngestResponse response)
    {
        if (_jobs.TryGetValue(jobId, out var job))
        {
            job.State = IngestJobState.Succeeded;
            job.CompletedAtUtc = DateTimeOffset.UtcNow;
            job.Response = response;
            _activeJobsByKnowledgeBase.TryRemove(job.KnowledgeBaseId, out _);
        }
    }

    public void MarkFailed(Guid jobId, string errorMessage)
    {
        if (_jobs.TryGetValue(jobId, out var job))
        {
            job.State = IngestJobState.Failed;
            job.CompletedAtUtc = DateTimeOffset.UtcNow;
            job.ErrorMessage = errorMessage;
            _activeJobsByKnowledgeBase.TryRemove(job.KnowledgeBaseId, out _);
        }
    }

    public bool TryCancel(Guid jobId)
    {
        if (_jobs.TryGetValue(jobId, out var job) && job.State == IngestJobState.Queued)
        {
            job.State = IngestJobState.Canceled;
            job.CompletedAtUtc = DateTimeOffset.UtcNow;
            job.ErrorMessage = "job canceled by operator";
            _activeJobsByKnowledgeBase.TryRemove(job.KnowledgeBaseId, out _);
            return true;
        }

        return false;
    }

    public bool TryUpdatePriority(Guid jobId, int priority)
    {
        if (_jobs.TryGetValue(jobId, out var job) && job.State == IngestJobState.Queued)
        {
            job.Priority = Math.Clamp(priority, -10, 10);
            return true;
        }

        return false;
    }

    public void ClearActiveJob(string knowledgeBaseId, Guid jobId)
    {
        _activeJobsByKnowledgeBase.TryRemove(knowledgeBaseId, out _);
    }
}

public interface IIngestJobDispatcher
{
    Task<IngestJobSubmission> SubmitAsync(IngestJobRequest request, CancellationToken cancellationToken);

    Task<IngestJobSubmission> RetryFailedAsync(Guid failedJobId, CancellationToken cancellationToken);
}

public interface IIngestProcessingService
{
    Task<IngestResponse> ProcessAsync(IngestJobRequest request, CancellationToken cancellationToken);
}

public sealed class IngestJobDispatcher(
    Channel<IngestJobRequest> channel,
    IIngestProcessingService processingService,
    IIngestJobStatusStore jobStatusStore,
    IOptions<IngestJobsOptions> ingestJobsOptions,
    ILogger<IngestJobDispatcher> logger) : IIngestJobDispatcher
{
    private readonly SemaphoreSlim _submitLock = new(1, 1);

    public async Task<IngestJobSubmission> SubmitAsync(IngestJobRequest request, CancellationToken cancellationToken)
    {
        await _submitLock.WaitAsync(cancellationToken);
        try
        {
            if (jobStatusStore.HasActiveJob(request.KnowledgeBaseId))
            {
                throw new InvalidOperationException($"An ingest job is already active for knowledge base '{request.KnowledgeBaseId}'.");
            }

            var jobId = request.JobId == Guid.Empty ? Guid.NewGuid() : request.JobId;
            var queuedResponse = BuildQueuedResponse(request, jobId);
            var submittedRequest = request with { JobId = jobId };
            jobStatusStore.MarkQueued(jobId, request.KnowledgeBaseId, queuedResponse, submittedRequest);

            if (!ingestJobsOptions.Value.IsBackground)
            {
                logger.LogInformation("Running ingest job synchronously. JobId={JobId}, KnowledgeBaseId={KnowledgeBaseId}", jobId, request.KnowledgeBaseId);
                jobStatusStore.MarkRunning(jobId);
                try
                {
                    var response = await processingService.ProcessAsync(submittedRequest, cancellationToken);
                    jobStatusStore.MarkSucceeded(jobId, response);
                    return new IngestJobSubmission(jobId, response, false);
                }
                catch (Exception ex)
                {
                    jobStatusStore.MarkFailed(jobId, ex.Message);
                    throw;
                }
            }

            await channel.Writer.WriteAsync(submittedRequest, cancellationToken);
            logger.LogInformation("Queued ingest job. JobId={JobId}, KnowledgeBaseId={KnowledgeBaseId}", jobId, request.KnowledgeBaseId);
            return new IngestJobSubmission(jobId, queuedResponse, true);
        }
        finally
        {
            _submitLock.Release();
        }
    }

    public async Task<IngestJobSubmission> RetryFailedAsync(Guid failedJobId, CancellationToken cancellationToken)
    {
        var failedJob = jobStatusStore.Get(failedJobId);
        if (failedJob is null || failedJob.State != IngestJobState.Failed)
        {
            throw new InvalidOperationException($"Ingest job '{failedJobId}' is not eligible for retry.");
        }

        var snapshot = jobStatusStore.GetRequestSnapshot(failedJobId);
        if (snapshot is null)
        {
            throw new InvalidOperationException($"No retry payload found for ingest job '{failedJobId}'.");
        }

        var retryRequest = snapshot with
        {
            JobId = Guid.Empty,
            RetryOfJobId = failedJobId
        };

        return await SubmitAsync(retryRequest, cancellationToken);
    }

    private static IngestResponse BuildQueuedResponse(IngestJobRequest request, Guid jobId) => new()
    {
        Accepted = true,
        Message = "Ingest job queued. Use the job status endpoint to monitor progress.",
        SourcePath = request.DisplayPath,
        ChunksCreated = 0,
        RecordsPersisted = 0,
        VectorStorePath = string.Empty,
        KnowledgeBaseId = request.KnowledgeBaseId,
        IsPlaceholder = true,
        JobId = jobId,
        JobStatus = "queued",
        JobStatusUrl = $"/api/v1/ingest/jobs/{jobId}"
    };
}

public sealed class IngestJobBackgroundService(
    Channel<IngestJobRequest> channel,
    IIngestProcessingService processingService,
    IIngestJobStatusStore jobStatusStore,
    ILogger<IngestJobBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var job in channel.Reader.ReadAllAsync(stoppingToken))
        {
            var status = jobStatusStore.Get(job.JobId);
            if (status?.State == IngestJobState.Canceled)
            {
                continue;
            }

            jobStatusStore.MarkRunning(job.JobId);

            try
            {
                var response = await processingService.ProcessAsync(job, stoppingToken);
                jobStatusStore.MarkSucceeded(job.JobId, response);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                jobStatusStore.MarkFailed(job.JobId, "job cancelled");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Ingest job failed. JobId={JobId}, KnowledgeBaseId={KnowledgeBaseId}", job.JobId, job.KnowledgeBaseId);
                jobStatusStore.MarkFailed(job.JobId, ex.Message);
            }
        }
    }
}

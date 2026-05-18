using Api.Contracts;
using Api.Controllers;
using Api.Services;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace Api.Tests;

public sealed class IngestJobsControllerTests
{
    [Fact(DisplayName = "Given an unknown job id when querying status then endpoint returns not found")]
    public void GivenUnknownJob_WhenGettingStatus_ThenReturnsNotFound()
    {
        var controller = new IngestJobsController(
            Mock.Of<IIngestJobDispatcher>(),
            new InMemoryIngestJobStatusStore());

        var result = controller.GetJobStatus(Guid.NewGuid());

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact(DisplayName = "Given a queued job when canceling then endpoint returns canceled status")]
    public void GivenQueuedJob_WhenCanceling_ThenReturnsCanceledJob()
    {
        var store = new InMemoryIngestJobStatusStore();
        var jobId = Guid.NewGuid();
        var queuedResponse = new IngestResponse
        {
            Accepted = true,
            Message = "queued",
            JobId = jobId,
            JobStatus = "queued",
            JobStatusUrl = $"/api/v1/ingest/jobs/{jobId}"
        };
        store.MarkQueued(jobId, "default", queuedResponse, CreateJobRequest(jobId));

        var controller = new IngestJobsController(Mock.Of<IIngestJobDispatcher>(), store);

        var result = controller.CancelJob(jobId);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<IngestJobStatusResponse>(ok.Value);
        Assert.Equal("canceled", payload.Status);
    }

    [Fact(DisplayName = "Given a failed job when retrying then endpoint returns successful retry submission")]
    public async Task GivenFailedJob_WhenRetrying_ThenReturnsSubmissionResponse()
    {
        var jobId = Guid.NewGuid();
        var submissionResponse = new IngestResponse
        {
            Accepted = true,
            Message = "queued",
            JobId = jobId,
            JobStatus = "queued"
        };

        var dispatcher = new Mock<IIngestJobDispatcher>();
        dispatcher
            .Setup(d => d.RetryFailedAsync(jobId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IngestJobSubmission(jobId, submissionResponse, true));

        var controller = new IngestJobsController(dispatcher.Object, new InMemoryIngestJobStatusStore());

        var result = await controller.RetryJob(jobId, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<IngestResponse>(ok.Value);
        Assert.True(payload.Accepted);
        Assert.Equal(jobId, payload.JobId);
    }

    [Fact(DisplayName = "Given a queued job when updating priority then endpoint returns updated priority")]
    public void GivenQueuedJob_WhenUpdatingPriority_ThenReturnsUpdatedPriority()
    {
        var store = new InMemoryIngestJobStatusStore();
        var jobId = Guid.NewGuid();
        var queuedResponse = new IngestResponse
        {
            Accepted = true,
            Message = "queued",
            JobId = jobId,
            JobStatus = "queued"
        };
        store.MarkQueued(jobId, "default", queuedResponse, CreateJobRequest(jobId));

        var controller = new IngestJobsController(Mock.Of<IIngestJobDispatcher>(), store);

        var result = controller.UpdateJobPriority(jobId, new IngestJobPriorityUpdateRequest { Priority = 7 });

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<IngestJobStatusResponse>(ok.Value);
        Assert.Equal(7, payload.Priority);
    }

    private static IngestJobRequest CreateJobRequest(Guid jobId)
    {
        return new IngestJobRequest(
            jobId,
            "default-ingest",
            "default",
            "content",
            "source.md",
            "source.md",
            [],
            [],
            null,
            false);
    }
}
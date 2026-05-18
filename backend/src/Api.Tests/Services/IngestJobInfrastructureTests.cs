using Api.Contracts;
using Api.Options;
using Api.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Threading.Channels;
using Xunit;

namespace Api.Tests.Services;

public sealed class IngestJobInfrastructureTests
{
    [Fact]
    public async Task BackgroundSubmission_AllowsPriorityUpdateAndCancel()
    {
        var channel = Channel.CreateUnbounded<IngestJobRequest>();
        var statusStore = new InMemoryIngestJobStatusStore();
        var processing = new StubIngestProcessingService();
        var dispatcher = new IngestJobDispatcher(
            channel,
            processing,
            statusStore,
            Microsoft.Extensions.Options.Options.Create(new IngestJobsOptions { Mode = "background" }),
            NullLogger<IngestJobDispatcher>.Instance);

        var submission = await dispatcher.SubmitAsync(CreateRequest("default"), CancellationToken.None);

        Assert.True(submission.IsBackground);
        var queued = Assert.IsType<IngestJobStatus>(statusStore.Get(submission.JobId));
        Assert.Equal(IngestJobState.Queued, queued.State);

        Assert.True(statusStore.TryUpdatePriority(submission.JobId, 5));
        Assert.Equal(5, statusStore.Get(submission.JobId)?.Priority);

        Assert.True(statusStore.TryCancel(submission.JobId));
        Assert.Equal(IngestJobState.Canceled, statusStore.Get(submission.JobId)?.State);
    }

    [Fact]
    public async Task RetryFailedAsync_RequeuesFailedJob()
    {
        var channel = Channel.CreateUnbounded<IngestJobRequest>();
        var statusStore = new InMemoryIngestJobStatusStore();
        var processing = new SequenceIngestProcessingService();
        var dispatcher = new IngestJobDispatcher(
            channel,
            processing,
            statusStore,
            Microsoft.Extensions.Options.Options.Create(new IngestJobsOptions { Mode = "sync" }),
            NullLogger<IngestJobDispatcher>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            dispatcher.SubmitAsync(CreateRequest("default"), CancellationToken.None));

        var failedJob = statusStore.List(1).Single();
        Assert.Equal(IngestJobState.Failed, failedJob.State);

        var retrySubmission = await dispatcher.RetryFailedAsync(failedJob.JobId, CancellationToken.None);

        Assert.False(retrySubmission.IsBackground);
        Assert.Equal("retry success", retrySubmission.Response.Message);
    }

    private static IngestJobRequest CreateRequest(string knowledgeBaseId)
    {
        return new IngestJobRequest(
            Guid.NewGuid(),
            "default-ingest",
            knowledgeBaseId,
            "# SOP",
            "SOP.md",
            "SOP.md",
            [],
            [],
            null,
            false);
    }

    private sealed class StubIngestProcessingService : IIngestProcessingService
    {
        public Task<IngestResponse> ProcessAsync(IngestJobRequest request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new IngestResponse
            {
                Accepted = true,
                Message = "ok",
                SourcePath = request.DisplayPath,
                ChunksCreated = 1,
                RecordsPersisted = 1,
                VectorStorePath = "Data/vector-store.json",
                KnowledgeBaseId = request.KnowledgeBaseId,
                IsPlaceholder = false
            });
        }
    }

    private sealed class SequenceIngestProcessingService : IIngestProcessingService
    {
        private int _callCount;

        public Task<IngestResponse> ProcessAsync(IngestJobRequest request, CancellationToken cancellationToken)
        {
            _callCount++;
            if (_callCount == 1)
            {
                throw new InvalidOperationException("first attempt failed");
            }

            return Task.FromResult(new IngestResponse
            {
                Accepted = true,
                Message = "retry success",
                SourcePath = request.DisplayPath,
                ChunksCreated = 1,
                RecordsPersisted = 1,
                VectorStorePath = "Data/vector-store.json",
                KnowledgeBaseId = request.KnowledgeBaseId,
                IsPlaceholder = false
            });
        }
    }
}

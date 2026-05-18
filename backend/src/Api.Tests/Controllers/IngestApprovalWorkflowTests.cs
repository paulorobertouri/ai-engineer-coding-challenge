using Api.Contracts;
using Api.Controllers;
using Api.Models;
using Api.Options;
using Api.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Threading.Channels;
using Xunit;

namespace Api.Tests.Controllers;

public sealed class IngestApprovalWorkflowTests
{
    [Fact]
    public async Task Post_InProduction_RequiresApprovalBeforeIngest()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"ingest-approval-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var sourcePath = Path.Combine(tempDir, "SOP.md");
        await File.WriteAllTextAsync(sourcePath, "# SOP\ncontent");

        var sourceText = "# SOP\ncontent";
        var sourceChecksum = DocumentVersioning.ComputeSourceChecksum(sourceText);

        var challengeOptions = Microsoft.Extensions.Options.Options.Create(new ChallengeOptions
        {
            SourceDocumentPath = sourcePath,
            VectorStorePath = Path.Combine(tempDir, "store.json")
        });

        var uploadOptions = Microsoft.Extensions.Options.Options.Create(new UploadOptions());
        var timeoutOptions = Microsoft.Extensions.Options.Options.Create(new TimeoutOptions { IngestSeconds = 120 });

        var chunking = new Mock<IChunkingService>();
        chunking
            .Setup(service => service.ChunkAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new TextChunk { Id = "chunk-1", Source = "SOP.md", Index = 0, Content = "# SOP\ncontent" }
            ]);

        var extraction = new Mock<IDocumentExtractionService>();
        extraction
            .Setup(service => service.ExtractTextFromFileAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(sourceText);
        extraction
            .Setup(service => service.IsSupportedExtension(It.IsAny<string>()))
            .Returns(true);
        extraction
            .Setup(service => service.DescribeSupportedFormats())
            .Returns(".md");

        var vectorStore = new Mock<IVectorStoreService>();
        vectorStore
            .Setup(service => service.LoadAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        vectorStore
            .Setup(service => service.SaveAsync(It.IsAny<IEnumerable<VectorRecord>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var audit = new Mock<IIngestionAuditService>();
        audit.Setup(service => service.RecordSuccessAsync(It.IsAny<IngestionAuditRecord>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        audit.Setup(service => service.RecordFailureAsync(It.IsAny<IngestionAuditRecord>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var env = new Mock<IWebHostEnvironment>();
        env.SetupGet(value => value.ContentRootPath).Returns(tempDir);
        env.SetupGet(value => value.EnvironmentName).Returns("Production");

        var approvalService = new InMemorySopMutationApprovalService();
        var ingestJobStatusStore = new InMemoryIngestJobStatusStore();
        var processing = new IngestProcessingService(
            challengeOptions,
            chunking.Object,
            new DeterministicEmbeddingService(),
            vectorStore.Object,
            audit.Object,
            NullLogger<IngestProcessingService>.Instance,
            env.Object);
        var dispatcher = new IngestJobDispatcher(
            Channel.CreateUnbounded<IngestJobRequest>(),
            processing,
            ingestJobStatusStore,
            Microsoft.Extensions.Options.Options.Create(new IngestJobsOptions { Mode = "sync" }),
            NullLogger<IngestJobDispatcher>.Instance);

        var controller = new IngestController(
            challengeOptions,
            uploadOptions,
            timeoutOptions,
            chunking.Object,
            extraction.Object,
            vectorStore.Object,
            approvalService,
            audit.Object,
            dispatcher,
            ingestJobStatusStore,
            NullLogger<IngestController>.Instance,
            env.Object);

        var blockedResult = await controller.Post(null, CancellationToken.None);
        Assert.IsType<ConflictObjectResult>(blockedResult.Result);

        var approvalResponse = controller.ApproveSopMutation(new SopMutationApprovalRequest
        {
            KnowledgeBaseId = "default",
            SourceChecksum = sourceChecksum,
            Note = "approved by test"
        });
        Assert.IsType<OkObjectResult>(approvalResponse.Result);

        var allowedResult = await controller.Post(null, CancellationToken.None);
        Assert.IsType<OkObjectResult>(allowedResult.Result);
    }
}

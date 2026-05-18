using Api.Contracts;
using Api.Application.Sources;
using Api.Models;
using Api.Options;
using Api.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Api.Tests;

public sealed class SourcesControllerTests
{
    private static SourcesEndpointsHandler CreateController(
        Mock<ISourceDocumentViewerService> sourceService,
        Mock<IVectorStoreService>? vectorStoreService = null,
        Mock<IChunkingService>? chunkingService = null,
        Mock<IDocumentExtractionService>? documentExtractionService = null,
        string? sourceDocumentPath = null,
        string? contentRootPath = null)
    {
        if (vectorStoreService is null)
        {
            vectorStoreService = new Mock<IVectorStoreService>();
            vectorStoreService
                .Setup(service => service.LoadAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync([]);
        }

        if (chunkingService is null)
        {
            chunkingService = new Mock<IChunkingService>();
            chunkingService
                .Setup(service => service.ChunkAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync([]);
        }

        if (documentExtractionService is null)
        {
            documentExtractionService = new Mock<IDocumentExtractionService>();
            documentExtractionService
                .Setup(service => service.ExtractTextFromFileAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(string.Empty);
        }

        var resolvedContentRoot = contentRootPath ?? Path.GetTempPath();
        var challengeOptions = Microsoft.Extensions.Options.Options.Create(new ChallengeOptions
        {
            SourceDocumentPath = sourceDocumentPath ?? "knowledge-base/Grocery_Store_SOP.md",
            VectorStorePath = "Data/vector-store.json"
        });

        var mockEnv = new Mock<IWebHostEnvironment>();
        mockEnv.SetupGet(env => env.ContentRootPath).Returns(resolvedContentRoot);

        return new SourcesEndpointsHandler(
            sourceService.Object,
            vectorStoreService.Object,
            chunkingService.Object,
            documentExtractionService.Object,
            challengeOptions,
            mockEnv.Object);
    }

    [Fact]
    public async Task GetDocument_MissingSource_ReturnsBadRequest()
    {
        var service = new Mock<ISourceDocumentViewerService>();
        var controller = CreateController(service);

        var result = await controller.GetDocument("", "default", CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
        service.Verify(
            dependency => dependency.GetDocumentAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetDocument_NotFound_ReturnsNotFound()
    {
        var service = new Mock<ISourceDocumentViewerService>();
        service
            .Setup(dependency => dependency.GetDocumentAsync("Grocery_Store_SOP.md", "default", It.IsAny<CancellationToken>()))
            .ReturnsAsync((SourceDocumentResponse?)null);

        var controller = CreateController(service);

        var result = await controller.GetDocument("Grocery_Store_SOP.md", "default", CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetDocument_Found_ReturnsOkDocument()
    {
        var expected = new SourceDocumentResponse
        {
            Source = "Grocery_Store_SOP.md",
            KnowledgeBaseId = "default",
            Chunks =
            [
                new SourceDocumentChunkDto
                {
                    ChunkId = "chunk-1",
                    Content = "Open the store at 7am.",
                    StartLine = 10,
                    EndLine = 12,
                    Index = 1
                }
            ]
        };

        var service = new Mock<ISourceDocumentViewerService>();
        service
            .Setup(dependency => dependency.GetDocumentAsync("Grocery_Store_SOP.md", "default", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var controller = CreateController(service);

        var result = await controller.GetDocument("Grocery_Store_SOP.md", "default", CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<SourceDocumentResponse>(ok.Value);
        Assert.Equal("Grocery_Store_SOP.md", payload.Source);
        Assert.Single(payload.Chunks);
    }

    [Fact]
    public async Task GetUpdateAlert_WhenSourceMatchesIngestedChecksum_ReturnsNoAlert()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"sources-alert-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var sourcePath = Path.Combine(tempDir, "SOP.md");
        var sourceContent = "# SOP\ncontent";
        await File.WriteAllTextAsync(sourcePath, sourceContent);
        var checksum = DocumentVersioning.ComputeSourceChecksum(sourceContent);

        var sourceService = new Mock<ISourceDocumentViewerService>();
        var vectorStore = new Mock<IVectorStoreService>();
        vectorStore
            .Setup(service => service.LoadAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new VectorRecord
                {
                    Id = "chunk-1",
                    Source = "SOP.md",
                    ChunkText = "chunk",
                    Embedding = [0.1f],
                    Metadata = new Dictionary<string, string>
                    {
                        [KnowledgeBaseScope.MetadataKey] = "default",
                        [DocumentVersioning.SourceChecksumMetadataKey] = checksum
                    }
                }
            ]);

        var controller = CreateController(
            sourceService,
            vectorStore,
            sourceDocumentPath: sourcePath,
            contentRootPath: tempDir);
        var result = await controller.GetUpdateAlert("default", CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<SourceUpdateAlertResponse>(ok.Value);
        Assert.False(payload.RequiresReingestReview);
    }

    [Fact]
    public async Task GetUpdateAlert_WhenChecksumDrifts_ReturnsAlert()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"sources-alert-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var sourcePath = Path.Combine(tempDir, "SOP.md");
        await File.WriteAllTextAsync(sourcePath, "# SOP\nnew content");

        var sourceService = new Mock<ISourceDocumentViewerService>();
        var vectorStore = new Mock<IVectorStoreService>();
        vectorStore
            .Setup(service => service.LoadAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new VectorRecord
                {
                    Id = "chunk-1",
                    Source = "SOP.md",
                    ChunkText = "chunk",
                    Embedding = [0.1f],
                    Metadata = new Dictionary<string, string>
                    {
                        [KnowledgeBaseScope.MetadataKey] = "default",
                        [DocumentVersioning.SourceChecksumMetadataKey] = "old-checksum"
                    }
                }
            ]);

        var controller = CreateController(
            sourceService,
            vectorStore,
            sourceDocumentPath: sourcePath,
            contentRootPath: tempDir);
        var result = await controller.GetUpdateAlert("default", CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<SourceUpdateAlertResponse>(ok.Value);
        Assert.True(payload.RequiresReingestReview);
        Assert.Equal("default", payload.KnowledgeBaseId);
    }

    [Fact]
    public async Task GetComparison_WhenChunkChangesExist_ReturnsDiffRows()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"sources-compare-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var sourcePath = Path.Combine(tempDir, "SOP.md");
        await File.WriteAllTextAsync(sourcePath, "# SOP\nnew content");

        var sourceService = new Mock<ISourceDocumentViewerService>();
        var vectorStore = new Mock<IVectorStoreService>();
        vectorStore
            .Setup(service => service.LoadAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new VectorRecord
                {
                    Id = "chunk-old-1",
                    Source = "SOP.md",
                    ChunkText = "old content",
                    Embedding = [0.1f],
                    Metadata = new Dictionary<string, string>
                    {
                        [KnowledgeBaseScope.MetadataKey] = "default",
                        ["Index"] = "0",
                        ["SectionTitle"] = "SOP",
                        [DocumentVersioning.DocumentVersionMetadataKey] = "sha256:old123"
                    }
                }
            ]);

        var chunkingService = new Mock<IChunkingService>();
        chunkingService
            .Setup(service => service.ChunkAsync(It.IsAny<string>(), "SOP.md", It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new TextChunk
                {
                    Id = "chunk-new-1",
                    Source = "SOP.md",
                    Content = "new content",
                    SectionTitle = "SOP",
                    Index = 0,
                    StartLine = 1,
                    EndLine = 2
                }
            ]);

        var extractor = new Mock<IDocumentExtractionService>();
        extractor
            .Setup(service => service.ExtractTextFromFileAsync(sourcePath, It.IsAny<CancellationToken>()))
            .ReturnsAsync("# SOP\nnew content");

        var controller = CreateController(
            sourceService,
            vectorStore,
            chunkingService,
            extractor,
            sourcePath,
            tempDir);

        var result = await controller.GetComparison("SOP.md", "default", "chunk-old-1", false, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<SourceComparisonResponse>(ok.Value);
        Assert.Equal("default", payload.KnowledgeBaseId);
        Assert.Equal(1, payload.ChangedChunkCount);
        Assert.Single(payload.Chunks);
        Assert.Equal("modified", payload.Chunks[0].ChangeType);
        Assert.True(payload.Chunks[0].IsImpactedCitation);
    }

    [Fact]
    public async Task GetQuality_ReturnsQualityMetrics()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"sources-quality-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var sourcePath = Path.Combine(tempDir, "SOP.md");
        await File.WriteAllTextAsync(sourcePath, "# SOP\ncontent");

        var sourceService = new Mock<ISourceDocumentViewerService>();
        var vectorStore = new Mock<IVectorStoreService>();
        vectorStore
            .Setup(service => service.LoadAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new VectorRecord
                {
                    Id = "chunk-1",
                    Source = "SOP.md",
                    ChunkText = "Short",
                    Embedding = [0.1f],
                    Metadata = new Dictionary<string, string>
                    {
                        [KnowledgeBaseScope.MetadataKey] = "default",
                        ["SectionTitle"] = "Opening",
                        ["StartLine"] = "1",
                        ["EndLine"] = "2"
                    }
                },
                new VectorRecord
                {
                    Id = "chunk-2",
                    Source = "SOP.md",
                    ChunkText = new string('x', 200),
                    Embedding = [0.1f],
                    Metadata = new Dictionary<string, string>
                    {
                        [KnowledgeBaseScope.MetadataKey] = "default",
                        ["SectionTitle"] = "Opening",
                        ["StartLine"] = "3",
                        ["EndLine"] = "10"
                    }
                }
            ]);

        var controller = CreateController(
            sourceService,
            vectorStore,
            sourceDocumentPath: sourcePath,
            contentRootPath: tempDir);

        var result = await controller.GetQuality("SOP.md", "default", CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<SourceQualityReportResponse>(ok.Value);
        Assert.Equal("default", payload.KnowledgeBaseId);
        Assert.Equal(2, payload.TotalChunks);
        Assert.Equal(1, payload.DuplicateSectionCount);
        Assert.Equal(1, payload.WeakExtractionZoneCount);
        Assert.NotEmpty(payload.ShortestChunks);
        Assert.NotEmpty(payload.LongestChunks);
    }
}

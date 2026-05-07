using Api.Contracts;
using Api.Controllers;
using Api.Models;
using Api.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests;

public class IngestControllerTests
{
    private readonly Mock<IConfiguration> _mockConfig = new();
    private readonly Mock<IChunkingService> _mockChunking = new();
    private readonly Mock<IEmbeddingService> _mockEmbedding = new();
    private readonly Mock<IVectorStoreService> _mockVectorStore = new();
    private readonly Mock<ILogger<IngestController>> _mockLogger = new();
    private readonly Mock<IWebHostEnvironment> _mockEnv = new();

    private string _tempDir = string.Empty;
    private string _sopFile = string.Empty;

    private IngestController BuildController(string? configuredPath = null)
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"ingest-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _sopFile = Path.Combine(_tempDir, "SOP.md");
        File.WriteAllText(_sopFile, "## Section\nContent");

        _mockConfig.Setup(c => c["Challenge:SourceDocumentPath"])
            .Returns(configuredPath ?? _sopFile);
        _mockConfig.Setup(c => c["Challenge:VectorStorePath"])
            .Returns(Path.Combine(_tempDir, "store.json"));

        _mockEnv.Setup(e => e.ContentRootPath).Returns(_tempDir);

        _mockChunking
            .Setup(s => s.ChunkAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new TextChunk { Id = "c1", Source = "SOP.md", Index = 0, Content = "## Section\nContent" }
            ]);

        _mockEmbedding
            .Setup(s => s.EmbedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new float[128]);

        _mockVectorStore
            .Setup(s => s.SaveAsync(It.IsAny<IEnumerable<VectorRecord>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        return new IngestController(
            _mockConfig.Object,
            _mockChunking.Object,
            _mockEmbedding.Object,
            _mockVectorStore.Object,
            _mockLogger.Object);
    }

    private void Cleanup()
    {
        if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public async Task Post_ValidSopFile_ReturnsOkWithIngestResponse()
    {
        var controller = BuildController();
        try
        {
            var result = await controller.Post(null, CancellationToken.None, _mockEnv.Object);

            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var response = Assert.IsType<IngestResponse>(ok.Value);
            Assert.True(response.Accepted);
            Assert.Equal(1, response.ChunksCreated);
            Assert.Equal(1, response.RecordsPersisted);
            Assert.False(response.IsPlaceholder);
        }
        finally { Cleanup(); }
    }

    [Fact]
    public async Task Post_WithDifferentCallerPath_LogsWarning()
    {
        var controller = BuildController();
        try
        {
            var request = new IngestRequest { SourcePath = "/some/other/path.md" };
            await controller.Post(request, CancellationToken.None, _mockEnv.Object);

            _mockLogger.Verify(
                l => l.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("overridden")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }
        finally { Cleanup(); }
    }

    [Fact]
    public async Task Post_FileNotFound_ReturnsNotFound()
    {
        var controller = BuildController("/nonexistent/path/SOP.md");
        try
        {
            var result = await controller.Post(null, CancellationToken.None, _mockEnv.Object);
            Assert.IsType<NotFoundObjectResult>(result.Result);
        }
        finally { Cleanup(); }
    }

    [Fact]
    public async Task Post_NullRequest_UsesConfiguredPath()
    {
        var controller = BuildController();
        try
        {
            var result = await controller.Post(null, CancellationToken.None, _mockEnv.Object);
            var ok = Assert.IsType<OkObjectResult>(result.Result);
            Assert.IsType<IngestResponse>(ok.Value);
        }
        finally { Cleanup(); }
    }

    [Fact]
    public async Task Post_SavesRecordsToVectorStore()
    {
        var controller = BuildController();
        try
        {
            await controller.Post(null, CancellationToken.None, _mockEnv.Object);

            _mockVectorStore.Verify(
                v => v.SaveAsync(It.IsAny<IEnumerable<VectorRecord>>(), It.IsAny<CancellationToken>()),
                Times.Once);
        }
        finally { Cleanup(); }
    }

    [Fact]
    public async Task Post_WithSamePathAsConfigured_DoesNotLogWarning()
    {
        var controller = BuildController();
        try
        {
            var request = new IngestRequest { SourcePath = _sopFile };
            await controller.Post(request, CancellationToken.None, _mockEnv.Object);

            _mockLogger.Verify(
                l => l.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Never);
        }
        finally { Cleanup(); }
    }

    [Fact]
    public async Task Post_UsesRelativePathFromContentRoot_WhenConfigIsRelative()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"ingest-rel-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _sopFile = Path.Combine(_tempDir, "SOP.md");
        File.WriteAllText(_sopFile, "## Rel Section\nContent");

        _mockConfig.Setup(c => c["Challenge:SourceDocumentPath"]).Returns("SOP.md");
        _mockConfig.Setup(c => c["Challenge:VectorStorePath"]).Returns("store.json");
        _mockEnv.Setup(e => e.ContentRootPath).Returns(_tempDir);

        _mockChunking
            .Setup(s => s.ChunkAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new TextChunk { Id = "c1", Source = "SOP.md", Index = 0, Content = "## Rel\nC" }]);
        _mockEmbedding
            .Setup(s => s.EmbedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new float[128]);
        _mockVectorStore
            .Setup(s => s.SaveAsync(It.IsAny<IEnumerable<VectorRecord>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var controller = new IngestController(
            _mockConfig.Object, _mockChunking.Object, _mockEmbedding.Object,
            _mockVectorStore.Object, _mockLogger.Object);

        try
        {
            var result = await controller.Post(null, CancellationToken.None, _mockEnv.Object);
            var ok = Assert.IsType<OkObjectResult>(result.Result);
            Assert.IsType<IngestResponse>(ok.Value);
        }
        finally { Cleanup(); }
    }

    [Fact]
    public async Task Post_FallsBackToLocalKnowledgeBase_WhenPrimaryPathMissing()
    {
        // Simulate: configured path missing, but local fallback ../../../../knowledge-base/SOP.md exists
        // We do this by placing a SOP.md 4 levels up from a deep contentRoot
        _tempDir = Path.Combine(Path.GetTempPath(), $"ingest-fb-{Guid.NewGuid():N}");
        var deepRoot = Path.Combine(_tempDir, "a", "b", "c", "d");
        Directory.CreateDirectory(deepRoot);

        var kbDir = Path.Combine(_tempDir, "knowledge-base");
        Directory.CreateDirectory(kbDir);
        var fbSop = Path.Combine(kbDir, "Grocery_Store_SOP.md");
        File.WriteAllText(fbSop, "## Fallback Section\nFallback content");

        _mockConfig.Setup(c => c["Challenge:SourceDocumentPath"]).Returns("/nonexistent/SOP.md");
        _mockConfig.Setup(c => c["Challenge:VectorStorePath"]).Returns("store.json");
        _mockEnv.Setup(e => e.ContentRootPath).Returns(deepRoot);

        _mockChunking
            .Setup(s => s.ChunkAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new TextChunk { Id = "fb1", Source = "Grocery_Store_SOP.md", Index = 0, Content = "## Fallback" }]);
        _mockEmbedding
            .Setup(s => s.EmbedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new float[128]);
        _mockVectorStore
            .Setup(s => s.SaveAsync(It.IsAny<IEnumerable<VectorRecord>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var controller = new IngestController(
            _mockConfig.Object, _mockChunking.Object, _mockEmbedding.Object,
            _mockVectorStore.Object, _mockLogger.Object);

        try
        {
            var result = await controller.Post(null, CancellationToken.None, _mockEnv.Object);
            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var response = Assert.IsType<IngestResponse>(ok.Value);
            Assert.True(response.Accepted);
        }
        finally
        {
            if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, recursive: true);
        }
    }
}

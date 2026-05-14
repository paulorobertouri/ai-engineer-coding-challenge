using Api.Contracts;
using Api.Controllers;
using Api.Models;
using Api.Options;
using Api.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Api.Tests;

public class IngestControllerTests
{
    private readonly Mock<IChunkingService> _mockChunking = new();
    private readonly Mock<IEmbeddingService> _mockEmbedding = new();
    private readonly Mock<IVectorStoreService> _mockVectorStore = new();
    private readonly Mock<ILogger<IngestController>> _mockLogger = new();
    private readonly Mock<IWebHostEnvironment> _mockEnv = new();

    private string _tempDir = string.Empty;
    private string _sopFile = string.Empty;

    private IngestController BuildController(string? configuredPath = null, bool isDevelopment = true)
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"ingest-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _sopFile = Path.Combine(_tempDir, "SOP.md");
        File.WriteAllText(_sopFile, "## Section\nContent");

        var challengeOptions = Microsoft.Extensions.Options.Options.Create(new ChallengeOptions
        {
            SourceDocumentPath = configuredPath ?? _sopFile,
            VectorStorePath = Path.Combine(_tempDir, "store.json")
        });
        var uploadOptions = Microsoft.Extensions.Options.Options.Create(new UploadOptions());
        var timeoutOptions = Microsoft.Extensions.Options.Options.Create(new TimeoutOptions { IngestSeconds = 120 });

        _mockEnv.Setup(e => e.ContentRootPath).Returns(_tempDir);
        _mockEnv.Setup(e => e.EnvironmentName).Returns(isDevelopment ? "Development" : "Production");

        _mockChunking
            .Setup(s => s.ChunkAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new TextChunk { Id = "c1", Source = "SOP.md", Index = 0, Content = "## Section\nContent", ContentHash = "hash-1" }
            ]);

        _mockEmbedding
            .Setup(s => s.EmbedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new float[128]);

        _mockVectorStore
            .Setup(s => s.LoadAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        _mockVectorStore
            .Setup(s => s.SaveAsync(It.IsAny<IEnumerable<VectorRecord>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        return new IngestController(
            challengeOptions,
            uploadOptions,
            timeoutOptions,
            _mockChunking.Object,
            _mockEmbedding.Object,
            _mockVectorStore.Object,
            _mockLogger.Object,
            _mockEnv.Object);
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
            var result = await controller.Post(null, CancellationToken.None);

            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var response = Assert.IsType<IngestResponse>(ok.Value);
            Assert.True(response.Accepted);
            Assert.Equal(1, response.ChunksCreated);
            Assert.Equal(1, response.RecordsPersisted);
            Assert.StartsWith("sha256:", response.DocumentVersion, StringComparison.Ordinal);
            Assert.NotEmpty(response.SourceChecksum);
            Assert.NotEqual(default, response.IngestedAtUtc);
            Assert.False(response.IsPlaceholder);
        }
        finally { Cleanup(); }
    }

    [Fact]
    public async Task Post_PersistsDocumentVersionMetadataOnSavedRecords()
    {
        var controller = BuildController();

        try
        {
            await controller.Post(null, CancellationToken.None);

            _mockVectorStore.Verify(
                v => v.SaveAsync(
                    It.Is<IEnumerable<VectorRecord>>(records =>
                        records.All(record =>
                            record.Metadata.ContainsKey("DocumentVersion") &&
                            record.Metadata.ContainsKey("SourceChecksum") &&
                            record.Metadata.ContainsKey("IngestedAtUtc"))),
                    It.IsAny<CancellationToken>()),
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
            var result = await controller.Post(null, CancellationToken.None);
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
            var result = await controller.Post(null, CancellationToken.None);
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
            await controller.Post(null, CancellationToken.None);

            _mockVectorStore.Verify(
                v => v.SaveAsync(It.IsAny<IEnumerable<VectorRecord>>(), It.IsAny<CancellationToken>()),
                Times.Once);
        }
        finally { Cleanup(); }
    }

    [Fact]
    public async Task Post_UsesRelativePathFromContentRoot_WhenConfigIsRelative()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"ingest-rel-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _sopFile = Path.Combine(_tempDir, "SOP.md");
        await File.WriteAllTextAsync(_sopFile, "## Rel Section\nContent");

        var challengeOptions = Microsoft.Extensions.Options.Options.Create(new ChallengeOptions
        {
            SourceDocumentPath = "SOP.md",
            VectorStorePath = "store.json"
        });
        var uploadOptions = Microsoft.Extensions.Options.Options.Create(new UploadOptions());
        var timeoutOptions = Microsoft.Extensions.Options.Options.Create(new TimeoutOptions { IngestSeconds = 120 });
        _mockEnv.Setup(e => e.ContentRootPath).Returns(_tempDir);

        _mockChunking
            .Setup(s => s.ChunkAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new TextChunk { Id = "c1", Source = "SOP.md", Index = 0, Content = "## Rel\nC" }]);
        _mockEmbedding
            .Setup(s => s.EmbedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new float[128]);
        _mockVectorStore
            .Setup(s => s.LoadAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        _mockVectorStore
            .Setup(s => s.SaveAsync(It.IsAny<IEnumerable<VectorRecord>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var controller = new IngestController(
            challengeOptions, uploadOptions, timeoutOptions, _mockChunking.Object, _mockEmbedding.Object,
            _mockVectorStore.Object, _mockLogger.Object, _mockEnv.Object);

        try
        {
            var result = await controller.Post(null, CancellationToken.None);
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
        await File.WriteAllTextAsync(fbSop, "## Fallback Section\nFallback content");

        var challengeOptions = Microsoft.Extensions.Options.Options.Create(new ChallengeOptions
        {
            SourceDocumentPath = "/nonexistent/SOP.md",
            VectorStorePath = "store.json"
        });
        var uploadOptions = Microsoft.Extensions.Options.Options.Create(new UploadOptions());
        var timeoutOptions = Microsoft.Extensions.Options.Options.Create(new TimeoutOptions { IngestSeconds = 120 });
        _mockEnv.Setup(e => e.ContentRootPath).Returns(deepRoot);

        _mockChunking
            .Setup(s => s.ChunkAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new TextChunk { Id = "fb1", Source = "Grocery_Store_SOP.md", Index = 0, Content = "## Fallback" }]);
        _mockEmbedding
            .Setup(s => s.EmbedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new float[128]);
        _mockVectorStore
            .Setup(s => s.LoadAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        _mockVectorStore
            .Setup(s => s.SaveAsync(It.IsAny<IEnumerable<VectorRecord>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var controller = new IngestController(
            challengeOptions, uploadOptions, timeoutOptions, _mockChunking.Object, _mockEmbedding.Object,
            _mockVectorStore.Object, _mockLogger.Object, _mockEnv.Object);

        try
        {
            var result = await controller.Post(null, CancellationToken.None);
            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var response = Assert.IsType<IngestResponse>(ok.Value);
            Assert.True(response.Accepted);
        }
        finally
        {
            if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task Post_WhenVectorStoreAlreadyHasRecords_ReturnsConflict()
    {
        var controller = BuildController();
        // Override LoadAsync to simulate an already-populated store
        _mockVectorStore
            .Setup(s => s.LoadAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new VectorRecord { Id = "existing", Source = "SOP.md", ChunkText = "x", Embedding = [] }]);

        try
        {
            var result = await controller.Post(null, CancellationToken.None);
            var conflict = Assert.IsType<ConflictObjectResult>(result.Result);
            var details = Assert.IsType<ProblemDetails>(conflict.Value);
            Assert.Equal(ApiErrorFactory.ConflictErrorCode, details.Extensions["code"]);

            _mockVectorStore.Verify(
                v => v.SaveAsync(It.IsAny<IEnumerable<VectorRecord>>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }
        finally { Cleanup(); }
    }

    [Fact]
    public async Task Post_WhenDifferentKnowledgeBaseHasRecords_AllowsIngest()
    {
        var controller = BuildController();
        _mockVectorStore
            .Setup(s => s.LoadAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new VectorRecord
                {
                    Id = "existing-hr",
                    Source = "HR.md",
                    ChunkText = "hr",
                    Embedding = [],
                    Metadata = new Dictionary<string, string> { ["KnowledgeBaseId"] = "hr" }
                }
            ]);

        try
        {
            var result = await controller.Post(new IngestRequest { ForceReingest = false, KnowledgeBaseId = "store" }, CancellationToken.None);
            Assert.IsType<OkObjectResult>(result.Result);

            _mockVectorStore.Verify(v => v.SaveAsync(
                It.Is<IEnumerable<VectorRecord>>(records =>
                    records.Any(r => r.Id == "existing-hr") &&
                    records.Any(r => r.Metadata.ContainsKey("KnowledgeBaseId") && r.Metadata["KnowledgeBaseId"] == "store")),
                It.IsAny<CancellationToken>()), Times.Once);
        }
        finally { Cleanup(); }
    }

    [Fact]
    public async Task Post_ConcurrentRequests_OnlyOneSucceeds()
    {
        var controller = BuildController();

        _mockVectorStore
            .SetupSequence(s => s.LoadAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([])
            .ReturnsAsync([new VectorRecord { Id = "existing", Source = "SOP.md", ChunkText = "x", Embedding = [] }]);

        var firstRequestStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirstRequest = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var chunkCallCount = 0;
        _mockChunking
            .Setup(s => s.ChunkAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                if (Interlocked.Increment(ref chunkCallCount) == 1)
                {
                    firstRequestStarted.SetResult();
                    await releaseFirstRequest.Task;
                }

                return [new TextChunk { Id = "c1", Source = "SOP.md", Index = 0, Content = "## Section\nContent" }];
            });

        try
        {
            var firstRequest = controller.Post(null, CancellationToken.None);
            await firstRequestStarted.Task;

            var secondRequest = controller.Post(null, CancellationToken.None);

            Assert.False(secondRequest.IsCompleted);

            releaseFirstRequest.SetResult();

            var firstResult = await firstRequest;
            var secondResult = await secondRequest;

            Assert.IsType<OkObjectResult>(firstResult.Result);
            Assert.IsType<ConflictObjectResult>(secondResult.Result);
        }
        finally { Cleanup(); }
    }

    // ── Upload endpoint ───────────────────────────────────────────────────────

    private static IFormFile MakeFormFile(string content, string fileName, string contentType = "text/markdown")
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(content);
        var stream = new MemoryStream(bytes);
        var formFile = new Microsoft.AspNetCore.Http.FormFile(stream, 0, bytes.Length, "file", fileName)
        {
            Headers = new Microsoft.AspNetCore.Http.HeaderDictionary(),
            ContentType = contentType
        };
        return formFile;
    }

    [Fact]
    public async Task Upload_ValidFile_ReturnsOkWithIngestResponse()
    {
        var controller = BuildController();
        var file = MakeFormFile("## Section\nContent here", "upload.md");
        try
        {
            var result = await controller.Upload(file, CancellationToken.None);

            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var response = Assert.IsType<IngestResponse>(ok.Value);
            Assert.True(response.Accepted);
            Assert.Equal(1, response.ChunksCreated);
        }
        finally { Cleanup(); }
    }

    [Fact]
    public async Task Upload_WhenVectorStoreAlreadyHasRecords_ReturnsConflict()
    {
        var controller = BuildController();
        _mockVectorStore
            .Setup(s => s.LoadAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new VectorRecord { Id = "x", Source = "s", ChunkText = "t", Embedding = [] }]);

        var file = MakeFormFile("content", "doc.md");
        try
        {
            var result = await controller.Upload(file, CancellationToken.None);
            Assert.IsType<ConflictObjectResult>(result.Result);
            _mockVectorStore.Verify(
                v => v.SaveAsync(It.IsAny<IEnumerable<VectorRecord>>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }
        finally { Cleanup(); }
    }

    [Fact]
    public async Task Upload_NullFile_ReturnsBadRequest()
    {
        var controller = BuildController();
        try
        {
            var result = await controller.Upload(null, CancellationToken.None);
            var bad = Assert.IsType<BadRequestObjectResult>(result.Result);
            var details = Assert.IsType<ProblemDetails>(bad.Value);
            Assert.Equal(ApiErrorFactory.BadRequestErrorCode, details.Extensions["code"]);
        }
        finally { Cleanup(); }
    }

    [Fact]
    public async Task Upload_EmptyFile_ReturnsBadRequest()
    {
        var controller = BuildController();
        var emptyFile = MakeFormFile("", "empty.md");
        try
        {
            var result = await controller.Upload(emptyFile, CancellationToken.None);
            Assert.IsType<BadRequestObjectResult>(result.Result);
        }
        finally { Cleanup(); }
    }

    [Fact]
    public async Task Upload_DisallowedExtension_ReturnsBadRequest()
    {
        var controller = BuildController();
        var file = MakeFormFile("content", "document.pdf", "application/pdf");
        try
        {
            var result = await controller.Upload(file, CancellationToken.None);
            var bad = Assert.IsType<BadRequestObjectResult>(result.Result);
            Assert.NotNull(bad.Value);
        }
        finally { Cleanup(); }
    }

    [Fact]
    public async Task Upload_TxtExtension_IsAccepted()
    {
        var controller = BuildController();
        var file = MakeFormFile("Plain text SOP content.", "procedures.txt", "text/plain");
        try
        {
            var result = await controller.Upload(file, CancellationToken.None);
            Assert.IsType<OkObjectResult>(result.Result);
        }
        finally { Cleanup(); }
    }

    [Fact]
    public async Task Upload_SavesRecordsToVectorStore()
    {
        var controller = BuildController();
        var file = MakeFormFile("## Section\nContent here", "upload.md");
        try
        {
            await controller.Upload(file, CancellationToken.None);
            _mockVectorStore.Verify(
                v => v.SaveAsync(It.IsAny<IEnumerable<VectorRecord>>(), It.IsAny<CancellationToken>()),
                Times.Once);
        }
        finally { Cleanup(); }
    }

    [Fact]
    public async Task Reset_NonDevelopment_ReturnsNotFound()
    {
        var controller = BuildController(isDevelopment: false);
        try
        {
            var result = await controller.Reset("RESET", CancellationToken.None);
            Assert.IsType<NotFoundObjectResult>(result.Result);
        }
        finally { Cleanup(); }
    }

    [Fact]
    public async Task Reset_DevelopmentWithoutConfirmation_ReturnsBadRequest()
    {
        var controller = BuildController();
        try
        {
            var result = await controller.Reset(null, CancellationToken.None);
            Assert.IsType<BadRequestObjectResult>(result.Result);
        }
        finally { Cleanup(); }
    }

    [Fact]
    public async Task Reset_DevelopmentWithConfirmation_ClearsVectorStore()
    {
        var controller = BuildController();
        _mockVectorStore
            .Setup(s => s.LoadAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new VectorRecord { Id = "existing", Source = "SOP.md", ChunkText = "x", Embedding = [] }]);

        try
        {
            var result = await controller.Reset("RESET", CancellationToken.None);
            Assert.True(result.Result is OkObjectResult || result.Value is not null);
            _mockVectorStore.Verify(
                v => v.SaveAsync(It.Is<IEnumerable<VectorRecord>>(records => !records.Any()), It.IsAny<CancellationToken>()),
                Times.Once);
        }
        finally { Cleanup(); }
    }

    [Fact]
    public async Task Post_ForceReingest_ReusesEmbeddingForUnchangedChunkHash()
    {
        var controller = BuildController();
        _mockVectorStore
            .Setup(s => s.LoadAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new VectorRecord
                {
                    Id = "old-1",
                    Source = "SOP.md",
                    ChunkText = "## Section\nContent",
                    Embedding = [0.25f, 0.5f],
                    Metadata = new Dictionary<string, string>
                    {
                        ["ContentHash"] = "hash-1"
                    }
                }
            ]);

        try
        {
            var result = await controller.Post(new IngestRequest { ForceReingest = true }, CancellationToken.None);
            Assert.IsType<OkObjectResult>(result.Result);

            _mockEmbedding.Verify(
                e => e.EmbedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
                Times.Never);
            _mockVectorStore.Verify(
                v => v.SaveAsync(It.IsAny<IEnumerable<VectorRecord>>(), It.IsAny<CancellationToken>()),
                Times.Once);
        }
        finally { Cleanup(); }
    }

    [Fact]
    public async Task Post_ForceReingest_PreservesUnchangedChunkRecord()
    {
        var controller = BuildController();
        var unchangedRecord = new VectorRecord
        {
            Id = "c1",
            Source = "SOP.md",
            ChunkText = "## Section\nContent",
            Embedding = [0.25f, 0.5f],
            Metadata = new Dictionary<string, string>
            {
                ["ContentHash"] = "hash-1",
                ["DocumentVersion"] = "sha256:oldversion",
                ["SourceChecksum"] = "checksum-old"
            }
        };

        _mockVectorStore
            .Setup(s => s.LoadAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([unchangedRecord]);

        try
        {
            var result = await controller.Post(new IngestRequest { ForceReingest = true }, CancellationToken.None);
            Assert.IsType<OkObjectResult>(result.Result);

            _mockEmbedding.Verify(
                e => e.EmbedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
                Times.Never);
            _mockVectorStore.Verify(v => v.SaveAsync(
                It.Is<IEnumerable<VectorRecord>>(records => ReferenceEquals(records.Single(), unchangedRecord)),
                It.IsAny<CancellationToken>()), Times.Once);
        }
        finally { Cleanup(); }
    }

    [Fact]
    public async Task Post_ForceReingest_ReplacesOnlyTargetKnowledgeBase()
    {
        var controller = BuildController();
        _mockVectorStore
            .Setup(s => s.LoadAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new VectorRecord
                {
                    Id = "default-old",
                    Source = "SOP.md",
                    ChunkText = "old",
                    Embedding = [0.25f],
                    Metadata = new Dictionary<string, string>
                    {
                        ["KnowledgeBaseId"] = "default",
                        ["ContentHash"] = "hash-old"
                    }
                },
                new VectorRecord
                {
                    Id = "hr-1",
                    Source = "HR.md",
                    ChunkText = "hr",
                    Embedding = [0.5f],
                    Metadata = new Dictionary<string, string>
                    {
                        ["KnowledgeBaseId"] = "hr"
                    }
                }
            ]);

        try
        {
            var result = await controller.Post(new IngestRequest { ForceReingest = true, KnowledgeBaseId = "default" }, CancellationToken.None);
            Assert.IsType<OkObjectResult>(result.Result);

            _mockVectorStore.Verify(v => v.SaveAsync(
                It.Is<IEnumerable<VectorRecord>>(records =>
                    records.Any(r => r.Id == "hr-1") &&
                    records.Any(r => r.Id == "c1" && r.Metadata.ContainsKey("KnowledgeBaseId") && r.Metadata["KnowledgeBaseId"] == "default") &&
                    records.All(r => r.Id != "default-old")),
                It.IsAny<CancellationToken>()), Times.Once);
        }
        finally { Cleanup(); }
    }

    [Fact]
    public async Task Post_ForceReingest_ReembedsChangedChunkHash()
    {
        var controller = BuildController();
        _mockVectorStore
            .Setup(s => s.LoadAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new VectorRecord
                {
                    Id = "old-1",
                    Source = "SOP.md",
                    ChunkText = "## Section\nOld Content",
                    Embedding = [0.25f, 0.5f],
                    Metadata = new Dictionary<string, string>
                    {
                        ["ContentHash"] = "hash-old"
                    }
                }
            ]);

        try
        {
            var result = await controller.Post(new IngestRequest { ForceReingest = true }, CancellationToken.None);
            Assert.IsType<OkObjectResult>(result.Result);

            _mockEmbedding.Verify(
                e => e.EmbedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
                Times.Once);
        }
        finally { Cleanup(); }
    }

    [Fact]
    public async Task Post_ForceReingest_RemovesDeletedChunksFromSavedRecords()
    {
        var controller = BuildController();
        _mockVectorStore
            .Setup(s => s.LoadAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new VectorRecord
                {
                    Id = "c1",
                    Source = "SOP.md",
                    ChunkText = "## Section\nContent",
                    Embedding = [0.25f, 0.5f],
                    Metadata = new Dictionary<string, string> { ["ContentHash"] = "hash-1" }
                },
                new VectorRecord
                {
                    Id = "deleted-chunk",
                    Source = "SOP.md",
                    ChunkText = "## Removed\nGone",
                    Embedding = [0.75f, 0.5f],
                    Metadata = new Dictionary<string, string> { ["ContentHash"] = "hash-deleted" }
                }
            ]);

        try
        {
            var result = await controller.Post(new IngestRequest { ForceReingest = true }, CancellationToken.None);
            Assert.IsType<OkObjectResult>(result.Result);

            _mockVectorStore.Verify(v => v.SaveAsync(
                It.Is<IEnumerable<VectorRecord>>(records =>
                    records.Any(r => r.Id == "c1") && records.All(r => r.Id != "deleted-chunk")),
                It.IsAny<CancellationToken>()), Times.Once);
        }
        finally { Cleanup(); }
    }

    [Fact]
    public async Task Post_WhenChunkingCancels_ReturnsRequestTimeoutProblemDetails()
    {
        var controller = BuildController();
        _mockChunking
            .Setup(s => s.ChunkAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        try
        {
            var result = await controller.Post(null, CancellationToken.None);
            var timeout = Assert.IsType<ObjectResult>(result.Result);
            Assert.Equal(StatusCodes.Status408RequestTimeout, timeout.StatusCode);
            var details = Assert.IsType<ProblemDetails>(timeout.Value);
            Assert.Equal(ApiErrorFactory.RequestTimeoutErrorCode, details.Extensions["code"]);
        }
        finally { Cleanup(); }
    }

    [Fact]
    public async Task Post_WhenRequestIsCancelled_ThrowsOperationCanceledException()
    {
        var controller = BuildController();
        _mockChunking
            .Setup(s => s.ChunkAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(async (string _, string _, CancellationToken token) =>
            {
                await Task.Delay(10, token);
                return [new TextChunk { Id = "c1", Source = "SOP.md", Index = 0, Content = "## Section\nContent", ContentHash = "hash-1" }];
            });

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => controller.Post(null, cts.Token));
    }
}

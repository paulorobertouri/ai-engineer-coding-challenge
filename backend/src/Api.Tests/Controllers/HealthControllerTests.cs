using Api.Contracts;
using Api.Controllers;
using Api.Models;
using Api.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace Api.Tests;

public class HealthControllerTests
{
    private static HealthController CreateController(bool hasApiKey = true, int recordCount = 0)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"health-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        var sourcePath = Path.Combine(tempDir, "SOP.md");
        File.WriteAllText(sourcePath, "# SOP\ncontent");

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OpenAI:ApiKey"] = hasApiKey ? "test-key" : "",
                ["Challenge:SourceDocumentPath"] = sourcePath,
                ["Challenge:VectorStorePath"] = "Data/vector-store.json"
            })
            .Build();

        var mockEnv = new Mock<IWebHostEnvironment>();
        mockEnv.SetupGet(x => x.ContentRootPath).Returns(tempDir);

        var mockVectorStore = new Mock<IVectorStoreService>();
        var records = Enumerable.Range(0, recordCount)
            .Select(i => new VectorRecord { Id = $"r{i}", Source = "SOP.md", ChunkText = "x", Embedding = [] })
            .ToList();
        mockVectorStore
            .Setup(s => s.LoadAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(records);

        return new HealthController(config, mockVectorStore.Object, mockEnv.Object);
    }

    [Fact]
    public async Task Get_ReturnsOkResult()
    {
        var result = await CreateController().Get(CancellationToken.None);
        Assert.IsType<OkObjectResult>(result.Result);
    }

    [Fact]
    public async Task Get_ReturnsHealthResponse()
    {
        var result = await CreateController().Get(CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.IsType<HealthResponse>(ok.Value);
    }

    [Fact]
    public async Task Get_StatusIsOk()
    {
        var result = await CreateController().Get(CancellationToken.None);
        var ok = (OkObjectResult)result.Result!;
        var response = (HealthResponse)ok.Value!;
        Assert.Equal("ok", response.Status);
    }

    [Fact]
    public async Task Get_ServiceNameIsCorrect()
    {
        var result = await CreateController().Get(CancellationToken.None);
        var ok = (OkObjectResult)result.Result!;
        var response = (HealthResponse)ok.Value!;
        Assert.Equal("grocery-store-sop-assistant-api", response.Service);
    }

    [Fact]
    public async Task Get_NotesAreNotEmpty()
    {
        var result = await CreateController().Get(CancellationToken.None);
        var ok = (OkObjectResult)result.Result!;
        var response = (HealthResponse)ok.Value!;
        Assert.NotEmpty(response.Notes);
    }

    [Fact]
    public async Task Get_UtcTimeIsRecent()
    {
        var before = DateTimeOffset.UtcNow;
        var result = await CreateController().Get(CancellationToken.None);
        var after = DateTimeOffset.UtcNow;

        var ok = (OkObjectResult)result.Result!;
        var response = (HealthResponse)ok.Value!;

        Assert.True(response.UtcTime >= before && response.UtcTime <= after);
    }

    [Fact]
    public async Task Get_WithApiKey_NotesIndicateFullMode()
    {
        var result = await CreateController(hasApiKey: true).Get(CancellationToken.None);
        var ok = (OkObjectResult)result.Result!;
        var response = (HealthResponse)ok.Value!;
        Assert.Contains(response.Notes, n => n.Contains("fully operational", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Get_WithoutApiKey_NotesIndicateFallbackMode()
    {
        var result = await CreateController(hasApiKey: false).Get(CancellationToken.None);
        var ok = (OkObjectResult)result.Result!;
        var response = (HealthResponse)ok.Value!;
        Assert.Contains(response.Notes, n => n.Contains("fallback", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Get_WhenVectorStoreEmpty_IsIngestedIsFalse()
    {
        var result = await CreateController(recordCount: 0).Get(CancellationToken.None);
        var ok = (OkObjectResult)result.Result!;
        var response = (HealthResponse)ok.Value!;
        Assert.False(response.IsIngested);
        Assert.Equal(0, response.RecordCount);
    }

    [Fact]
    public async Task Get_WhenVectorStoreHasRecords_IsIngestedIsTrue()
    {
        var result = await CreateController(recordCount: 5).Get(CancellationToken.None);
        var ok = (OkObjectResult)result.Result!;
        var response = (HealthResponse)ok.Value!;
        Assert.True(response.IsIngested);
        Assert.Equal(5, response.RecordCount);
    }

    [Fact]
    public void Ready_WhenDependenciesAvailable_ReturnsOkReady()
    {
        var result = CreateController().Ready();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<HealthResponse>(ok.Value);
        Assert.Equal("ready", response.Status);
    }

    [Fact]
    public void Ready_WhenSourceDocumentMissing_ReturnsServiceUnavailable()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"health-tests-missing-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OpenAI:ApiKey"] = "",
                ["Challenge:SourceDocumentPath"] = Path.Combine(tempDir, "missing.md"),
                ["Challenge:VectorStorePath"] = "Data/vector-store.json"
            })
            .Build();

        var mockEnv = new Mock<IWebHostEnvironment>();
        mockEnv.SetupGet(x => x.ContentRootPath).Returns(tempDir);

        var mockVectorStore = new Mock<IVectorStoreService>();
        mockVectorStore
            .Setup(s => s.LoadAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var controller = new HealthController(config, mockVectorStore.Object, mockEnv.Object);

        var result = controller.Ready();

        var objectResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(503, objectResult.StatusCode);

        var response = Assert.IsType<HealthResponse>(objectResult.Value);
        Assert.Equal("not_ready", response.Status);
    }
}

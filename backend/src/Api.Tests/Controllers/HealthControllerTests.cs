using Api.Contracts;
using Api.Controllers;
using Api.Options;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace Api.Tests;

public class HealthControllerTests
{
    private static HealthController CreateController(bool hasApiKey = true)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"health-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        var sourcePath = Path.Combine(tempDir, "SOP.md");
        File.WriteAllText(sourcePath, "# SOP\ncontent");

        var openAiOptions = Microsoft.Extensions.Options.Options.Create(new OpenAIOptions
        {
            ApiKey = hasApiKey ? "test-key" : string.Empty
        });
        var challengeOptions = Microsoft.Extensions.Options.Options.Create(new ChallengeOptions
        {
            SourceDocumentPath = sourcePath,
            VectorStorePath = "Data/vector-store.json"
        });
        var vectorStoreOptions = Microsoft.Extensions.Options.Options.Create(new VectorStoreOptions
        {
            Provider = "json"
        });
        var healthChecksOptions = Microsoft.Extensions.Options.Options.Create(new HealthChecksOptions
        {
            EnableOpenAIConnectivityProbe = false,
            OpenAIProbeHost = "api.openai.com",
            OpenAIProbeTimeoutMilliseconds = 1200
        });

        var mockEnv = new Mock<IWebHostEnvironment>();
        mockEnv.SetupGet(x => x.ContentRootPath).Returns(tempDir);

        return new HealthController(openAiOptions, challengeOptions, vectorStoreOptions, healthChecksOptions, mockEnv.Object);
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
    public async Task Get_LivenessIsLightweightWithDefaultKnowledgeBase()
    {
        var result = await CreateController().Get(CancellationToken.None);
        var ok = (OkObjectResult)result.Result!;
        var response = (HealthResponse)ok.Value!;

        Assert.False(response.IsIngested);
        Assert.Equal(0, response.RecordCount);
        Assert.Contains("default", response.ActiveKnowledgeBaseIds, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Get_ReportsReadinessHintInNotes()
    {
        var result = await CreateController().Get(CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<HealthResponse>(ok.Value);

        Assert.Contains(response.Notes, note => note.Contains("/api/v1/ready", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Ready_WhenDependenciesAvailable_ReturnsOkReady()
    {
        var result = await CreateController().Ready(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<HealthResponse>(ok.Value);
        Assert.Equal("ready", response.Status);
        Assert.Contains("default", response.ActiveKnowledgeBaseIds, StringComparer.OrdinalIgnoreCase);
        Assert.Contains(response.Notes, note => note.Contains("connectivity check skipped", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Ready_WhenSourceDocumentMissing_ReturnsServiceUnavailable()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"health-tests-missing-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        var openAiOptions = Microsoft.Extensions.Options.Options.Create(new OpenAIOptions
        {
            ApiKey = string.Empty
        });
        var challengeOptions = Microsoft.Extensions.Options.Options.Create(new ChallengeOptions
        {
            SourceDocumentPath = Path.Combine(tempDir, "missing.md"),
            VectorStorePath = "Data/vector-store.json"
        });
        var vectorStoreOptions = Microsoft.Extensions.Options.Options.Create(new VectorStoreOptions
        {
            Provider = "json"
        });
        var healthChecksOptions = Microsoft.Extensions.Options.Options.Create(new HealthChecksOptions
        {
            EnableOpenAIConnectivityProbe = false,
            OpenAIProbeHost = "api.openai.com",
            OpenAIProbeTimeoutMilliseconds = 1200
        });

        var mockEnv = new Mock<IWebHostEnvironment>();
        mockEnv.SetupGet(x => x.ContentRootPath).Returns(tempDir);

        var controller = new HealthController(openAiOptions, challengeOptions, vectorStoreOptions, healthChecksOptions, mockEnv.Object);

        var result = await controller.Ready(CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(503, objectResult.StatusCode);

        var response = Assert.IsType<HealthResponse>(objectResult.Value);
        Assert.Equal("not_ready", response.Status);
    }
}

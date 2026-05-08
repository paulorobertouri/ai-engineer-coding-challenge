using Api.Contracts;
using Api.Controllers;
using Api.Models;
using Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace Api.Tests;

public class HealthControllerTests
{
    private static HealthController CreateController(bool hasApiKey = true, int recordCount = 0)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OpenAI:ApiKey"] = hasApiKey ? "test-key" : ""
            })
            .Build();

        var mockVectorStore = new Mock<IVectorStoreService>();
        var records = Enumerable.Range(0, recordCount)
            .Select(i => new VectorRecord { Id = $"r{i}", Source = "SOP.md", ChunkText = "x", Embedding = [] })
            .ToList();
        mockVectorStore
            .Setup(s => s.LoadAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(records);

        return new HealthController(config, mockVectorStore.Object);
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
}

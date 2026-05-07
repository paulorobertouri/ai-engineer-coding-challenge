using Api.Contracts;
using Api.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Api.Tests;

public class HealthControllerTests
{
    private static HealthController CreateController(bool hasApiKey = true)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OpenAI:ApiKey"] = hasApiKey ? "test-key" : ""
            })
            .Build();
        return new HealthController(config);
    }

    [Fact]
    public void Get_ReturnsOkResult()
    {
        var result = CreateController().Get();
        Assert.IsType<OkObjectResult>(result.Result);
    }

    [Fact]
    public void Get_ReturnsHealthResponse()
    {
        var result = CreateController().Get();
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.IsType<HealthResponse>(ok.Value);
    }

    [Fact]
    public void Get_StatusIsOk()
    {
        var result = CreateController().Get();
        var ok = (OkObjectResult)result.Result!;
        var response = (HealthResponse)ok.Value!;
        Assert.Equal("ok", response.Status);
    }

    [Fact]
    public void Get_ServiceNameIsCorrect()
    {
        var result = CreateController().Get();
        var ok = (OkObjectResult)result.Result!;
        var response = (HealthResponse)ok.Value!;
        Assert.Equal("grocery-store-sop-assistant-api", response.Service);
    }

    [Fact]
    public void Get_NotesAreNotEmpty()
    {
        var result = CreateController().Get();
        var ok = (OkObjectResult)result.Result!;
        var response = (HealthResponse)ok.Value!;
        Assert.NotEmpty(response.Notes);
    }

    [Fact]
    public void Get_UtcTimeIsRecent()
    {
        var before = DateTimeOffset.UtcNow;
        var result = CreateController().Get();
        var after = DateTimeOffset.UtcNow;

        var ok = (OkObjectResult)result.Result!;
        var response = (HealthResponse)ok.Value!;

        Assert.True(response.UtcTime >= before && response.UtcTime <= after);
    }

    [Fact]
    public void Get_WithApiKey_NotesIndicateFullMode()
    {
        var result = CreateController(hasApiKey: true).Get();
        var ok = (OkObjectResult)result.Result!;
        var response = (HealthResponse)ok.Value!;
        Assert.Contains(response.Notes, n => n.Contains("fully operational", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Get_WithoutApiKey_NotesIndicateFallbackMode()
    {
        var result = CreateController(hasApiKey: false).Get();
        var ok = (OkObjectResult)result.Result!;
        var response = (HealthResponse)ok.Value!;
        Assert.Contains(response.Notes, n => n.Contains("fallback", StringComparison.OrdinalIgnoreCase));
    }
}

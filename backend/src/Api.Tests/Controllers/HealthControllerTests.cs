using Api.Contracts;
using Api.Controllers;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Api.Tests;

public class HealthControllerTests
{
    private readonly HealthController _controller = new();

    [Fact]
    public void Get_ReturnsOkResult()
    {
        var result = _controller.Get();
        Assert.IsType<OkObjectResult>(result.Result);
    }

    [Fact]
    public void Get_ReturnsHealthResponse()
    {
        var result = _controller.Get();
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.IsType<HealthResponse>(ok.Value);
    }

    [Fact]
    public void Get_StatusIsOk()
    {
        var result = _controller.Get();
        var ok = (OkObjectResult)result.Result!;
        var response = (HealthResponse)ok.Value!;
        Assert.Equal("ok", response.Status);
    }

    [Fact]
    public void Get_ServiceNameIsCorrect()
    {
        var result = _controller.Get();
        var ok = (OkObjectResult)result.Result!;
        var response = (HealthResponse)ok.Value!;
        Assert.Equal("grocery-store-sop-assistant-api", response.Service);
    }

    [Fact]
    public void Get_NotesAreNotEmpty()
    {
        var result = _controller.Get();
        var ok = (OkObjectResult)result.Result!;
        var response = (HealthResponse)ok.Value!;
        Assert.NotEmpty(response.Notes);
    }

    [Fact]
    public void Get_UtcTimeIsRecent()
    {
        var before = DateTimeOffset.UtcNow;
        var result = _controller.Get();
        var after = DateTimeOffset.UtcNow;

        var ok = (OkObjectResult)result.Result!;
        var response = (HealthResponse)ok.Value!;

        Assert.True(response.UtcTime >= before && response.UtcTime <= after);
    }
}

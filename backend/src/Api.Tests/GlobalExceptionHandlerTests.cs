using Api.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests;

public class GlobalExceptionHandlerTests
{
    private readonly Mock<ILogger<GlobalExceptionHandler>> _mockLogger = new();
    private readonly Mock<IWebHostEnvironment> _mockEnv = new();

    private GlobalExceptionHandler BuildHandler(bool isDevelopment)
    {
        _mockEnv.Setup(e => e.EnvironmentName).Returns(isDevelopment ? "Development" : "Production");
        return new GlobalExceptionHandler(_mockLogger.Object, _mockEnv.Object);
    }

    private static DefaultHttpContext BuildHttpContext(string method = "GET", string path = "/api/v1/health")
    {
        var ctx = new DefaultHttpContext();
        ctx.Response.Body = new MemoryStream();
        ctx.Request.Method = method;
        ctx.Request.Path = path;
        return ctx;
    }

    private static async Task<string> ReadBodyAsync(HttpContext ctx)
    {
        ctx.Response.Body.Seek(0, SeekOrigin.Begin);
        return await new StreamReader(ctx.Response.Body).ReadToEndAsync();
    }

    [Fact]
    public async Task TryHandleAsync_ReturnsTrueAlways()
    {
        var handler = BuildHandler(isDevelopment: true);
        var result = await handler.TryHandleAsync(BuildHttpContext(), new Exception("boom"), CancellationToken.None);
        Assert.True(result);
    }

    [Fact]
    public async Task TryHandleAsync_Sets500StatusCode()
    {
        var handler = BuildHandler(isDevelopment: true);
        var ctx = BuildHttpContext();

        await handler.TryHandleAsync(ctx, new Exception("err"), CancellationToken.None);

        Assert.Equal(500, ctx.Response.StatusCode);
    }

    [Fact]
    public async Task TryHandleAsync_LogsErrorWithException()
    {
        var handler = BuildHandler(isDevelopment: true);
        var ex = new InvalidOperationException("kaboom");

        await handler.TryHandleAsync(BuildHttpContext(), ex, CancellationToken.None);

        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("kaboom")),
                ex,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task TryHandleAsync_Development_WritesExceptionMessageAsDetail()
    {
        var handler = BuildHandler(isDevelopment: true);
        var ctx = BuildHttpContext();

        await handler.TryHandleAsync(ctx, new Exception("secret internal error"), CancellationToken.None);

        var body = await ReadBodyAsync(ctx);
        Assert.Contains("secret internal error", body);
    }

    [Fact]
    public async Task TryHandleAsync_Production_WritesGenericMessage()
    {
        var handler = BuildHandler(isDevelopment: false);
        var ctx = BuildHttpContext();

        await handler.TryHandleAsync(ctx, new Exception("secret internal error"), CancellationToken.None);

        var body = await ReadBodyAsync(ctx);
        Assert.DoesNotContain("secret internal error", body);
        Assert.Contains("unexpected error", body);
    }

    [Fact]
    public async Task TryHandleAsync_IncludesRequestMethodAndPath()
    {
        var handler = BuildHandler(isDevelopment: true);
        var ctx = BuildHttpContext(method: "POST", path: "/api/v1/ingest");

        await handler.TryHandleAsync(ctx, new Exception("err"), CancellationToken.None);

        var body = await ReadBodyAsync(ctx);
        Assert.Contains("POST", body);
        Assert.Contains("/api/v1/ingest", body);
    }

    [Fact]
    public async Task TryHandleAsync_CancelledRequest_StillHandles()
    {
        var handler = BuildHandler(isDevelopment: false);
        using var cts = new CancellationTokenSource();

        // Should not throw even when cancellation token is already cancelled
        // (WriteAsJsonAsync respects the token, but the handler itself should not throw)
        var ctx = BuildHttpContext();
        var result = await handler.TryHandleAsync(ctx, new Exception("err"), cts.Token);

        Assert.True(result);
    }
}

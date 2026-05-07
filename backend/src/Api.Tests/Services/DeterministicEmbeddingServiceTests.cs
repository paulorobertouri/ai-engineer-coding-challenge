using Api.Services;
using Xunit;

namespace Api.Tests;

public class DeterministicEmbeddingServiceTests
{
    private readonly DeterministicEmbeddingService _service = new();

    [Fact]
    public async Task EmbedAsync_ReturnsVectorOf128Dimensions()
    {
        var result = await _service.EmbedAsync("hello world");
        Assert.Equal(128, result.Length);
    }

    [Fact]
    public async Task EmbedAsync_ReturnsNormalizedVector()
    {
        var result = await _service.EmbedAsync("grocery store operations");
        var norm = Math.Sqrt(result.Sum(v => (double)v * v));
        Assert.Equal(1.0, norm, precision: 5);
    }

    [Fact]
    public async Task EmbedAsync_EmptyText_ReturnsZeroVector()
    {
        var result = await _service.EmbedAsync(string.Empty);
        Assert.All(result, v => Assert.Equal(0f, v));
    }

    [Fact]
    public async Task EmbedAsync_WhitespaceOnly_ReturnsZeroVector()
    {
        var result = await _service.EmbedAsync("   ");
        Assert.All(result, v => Assert.Equal(0f, v));
    }

    [Fact]
    public async Task EmbedAsync_SameTextProducesSameVector()
    {
        var r1 = await _service.EmbedAsync("consistency check");
        var r2 = await _service.EmbedAsync("consistency check");
        Assert.Equal(r1, r2);
    }

    [Fact]
    public async Task EmbedAsync_DifferentTextProducesDifferentVectors()
    {
        var r1 = await _service.EmbedAsync("apple");
        var r2 = await _service.EmbedAsync("banana");
        Assert.NotEqual(r1, r2);
    }

    [Fact]
    public async Task EmbedAsync_CancellationToken_IsAccepted()
    {
        using var cts = new CancellationTokenSource();
        var result = await _service.EmbedAsync("hello", cts.Token);
        Assert.Equal(128, result.Length);
    }
}

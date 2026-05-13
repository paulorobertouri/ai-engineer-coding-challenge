using Api.Options;
using Api.Services;
using Microsoft.Extensions.Options;
using Xunit;

namespace Api.Tests;

public class OpenAIEmbeddingServiceTests
{
    private static OpenAIEmbeddingService CreateService() => new(
        null!,
        Microsoft.Extensions.Options.Options.Create(new OpenAIOptions()));

    [Fact]
    public async Task EmbedAsync_EmptyText_ReturnsZeroVector()
    {
        var service = CreateService();

        var result = await service.EmbedAsync(string.Empty);

        Assert.Equal(1536, result.Length);
        Assert.All(result, value => Assert.Equal(0f, value));
    }

    [Fact]
    public async Task EmbedAsync_WhitespaceOnly_ReturnsZeroVector()
    {
        var service = CreateService();

        var result = await service.EmbedAsync("   ");

        Assert.Equal(1536, result.Length);
        Assert.All(result, value => Assert.Equal(0f, value));
    }
}
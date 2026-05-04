using Api.Services;
using Xunit;

namespace Api.Tests;

public class MarkdownChunkingServiceTests
{
    [Fact]
    public async Task ChunkAsync_SplitsByLevel2Headers()
    {
        // Arrange
        var service = new MarkdownChunkingService();
        var markdown = """
            # Title
            ## Section 1
            Content 1
            ## Section 2
            Content 2
            """;

        // Act
        var chunks = await service.ChunkAsync(markdown, "test.md");

        // Assert
        Assert.Equal(3, chunks.Count);
        Assert.Contains("# Title", chunks[0].Content);
        Assert.Contains("## Section 1", chunks[1].Content);
        Assert.Contains("## Section 2", chunks[2].Content);
    }

    [Fact]
    public async Task ChunkAsync_HandlesEmptyText()
    {
        // Arrange
        var service = new MarkdownChunkingService();
        var markdown = "";

        // Act
        var chunks = await service.ChunkAsync(markdown, "test.md");

        // Assert
        Assert.Empty(chunks);
    }
}
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

    [Fact]
    public async Task ChunkAsync_HandlesWhitespaceOnlyText()
    {
        var service = new MarkdownChunkingService();
        var chunks = await service.ChunkAsync("   \n\t  ", "test.md");
        Assert.Empty(chunks);
    }

    [Fact]
    public async Task ChunkAsync_OnlyH2Section_NoTitle()
    {
        var service = new MarkdownChunkingService();
        var markdown = "## Only Section\nSome content";
        var chunks = await service.ChunkAsync(markdown, "test.md");
        Assert.Single(chunks);
        Assert.Contains("## Only Section", chunks[0].Content);
    }

    [Fact]
    public async Task ChunkAsync_SetsSourceName()
    {
        var service = new MarkdownChunkingService();
        var chunks = await service.ChunkAsync("## Section\nContent", "my-doc.md");
        Assert.All(chunks, c => Assert.Equal("my-doc.md", c.Source));
    }

    [Fact]
    public async Task ChunkAsync_IndexIsSequential()
    {
        var service = new MarkdownChunkingService();
        var markdown = "# Title\n## S1\nA\n## S2\nB\n## S3\nC";
        var chunks = await service.ChunkAsync(markdown, "test.md");
        for (int i = 0; i < chunks.Count; i++)
            Assert.Equal(i, chunks[i].Index);
    }

    [Fact]
    public async Task ChunkAsync_EachChunkHasUniqueId()
    {
        var service = new MarkdownChunkingService();
        var markdown = "# Title\n## S1\nA\n## S2\nB";
        var chunks = await service.ChunkAsync(markdown, "test.md");
        var ids = chunks.Select(c => c.Id).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    [Fact]
    public async Task ChunkAsync_NoHeaders_ReturnsSingleChunk()
    {
        var service = new MarkdownChunkingService();
        var markdown = "Just some plain content without any headers.";
        var chunks = await service.ChunkAsync(markdown, "test.md");
        Assert.Single(chunks);
    }
}
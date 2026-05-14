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
    public async Task ChunkAsync_SetsSectionTitleAndLineRange()
    {
        var service = new MarkdownChunkingService();
        var markdown = "# Intro\nLine A\n## Policy\nLine B\nLine C";

        var chunks = await service.ChunkAsync(markdown, "test.md");

        Assert.Equal("Intro", chunks[0].SectionTitle);
        Assert.Equal(1, chunks[0].StartLine);
        Assert.Equal(2, chunks[0].EndLine);

        Assert.Equal("Policy", chunks[1].SectionTitle);
        Assert.Equal(3, chunks[1].StartLine);
        Assert.Equal(5, chunks[1].EndLine);
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
    public async Task ChunkAsync_SameInput_ProducesStableChunkIdsAndHashes()
    {
        var service = new MarkdownChunkingService();
        var markdown = "# Title\n## S1\nA\n## S2\nB";

        var first = await service.ChunkAsync(markdown, "test.md");
        var second = await service.ChunkAsync(markdown, "test.md");

        Assert.Equal(first.Select(c => c.Id), second.Select(c => c.Id));
        Assert.Equal(first.Select(c => c.ContentHash), second.Select(c => c.ContentHash));
    }

    [Fact]
    public async Task ChunkAsync_ContentChange_OnlyChangesAffectedChunkIdentity()
    {
        var service = new MarkdownChunkingService();
        var original = "## S1\nA\n## S2\nB";
        var updated = "## S1\nA\n## S2\nChanged";

        var first = await service.ChunkAsync(original, "test.md");
        var second = await service.ChunkAsync(updated, "test.md");

        Assert.Equal(first[0].Id, second[0].Id);
        Assert.Equal(first[0].ContentHash, second[0].ContentHash);

        Assert.NotEqual(first[1].Id, second[1].Id);
        Assert.NotEqual(first[1].ContentHash, second[1].ContentHash);
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
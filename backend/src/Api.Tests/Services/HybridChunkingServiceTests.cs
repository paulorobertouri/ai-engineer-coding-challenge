using Api.Services;
using Xunit;

namespace Api.Tests;

public class HybridChunkingServiceTests
{
    private readonly HybridChunkingService _sut = new();

    // ── Strategy 1: Header splitting ─────────────────────────────────────────

    [Fact]
    public async Task ChunkAsync_SplitsOnLevel1Headers()
    {
        var text = "# Title\nThis is the introductory content for the title section.\n# Section 2\nThis is the content for section two of the document.";
        var chunks = await _sut.ChunkAsync(text, "doc.md");
        Assert.Equal(2, chunks.Count);
        Assert.Contains("# Title", chunks[0].Content);
        Assert.Contains("# Section 2", chunks[1].Content);
    }

    [Fact]
    public async Task ChunkAsync_SplitsOnLevel2Headers()
    {
        var text = "## Alpha\nContent for the alpha subsection goes here.\n## Beta\nContent for the beta subsection goes here.\n## Gamma\nContent for the gamma subsection goes here.";
        var chunks = await _sut.ChunkAsync(text, "doc.md");
        Assert.Equal(3, chunks.Count);
        Assert.Contains("## Alpha", chunks[0].Content);
    }

    [Fact]
    public async Task ChunkAsync_SplitsOnMixedHeaderLevels()
    {
        var text = "# H1\nThis is level one heading content with sufficient length.\n## H2\nThis is level two heading content with sufficient length.\n### H3\nThis is level three heading content with sufficient length.";
        var chunks = await _sut.ChunkAsync(text, "doc.md");
        Assert.Equal(3, chunks.Count);
    }

    // ── Strategy 2: Paragraph splitting ──────────────────────────────────────

    [Fact]
    public async Task ChunkAsync_UseParagraphsWhenNoHeaders()
    {
        var text = "This is the first paragraph with enough content.\n\nThis is the second paragraph with enough content.\n\nThis is the third paragraph with enough content.";
        var chunks = await _sut.ChunkAsync(text, "plain.txt");
        Assert.Equal(3, chunks.Count);
        Assert.Contains("first paragraph", chunks[0].Content);
        Assert.Contains("second paragraph", chunks[1].Content);
    }

    [Fact]
    public async Task ChunkAsync_LargeHeaderSectionIsParagraphSplit()
    {
        // Build a section > 2000 chars with two clear paragraphs
        var para1 = string.Concat(Enumerable.Repeat("Word1 ", 200)); // ~1200 chars
        var para2 = string.Concat(Enumerable.Repeat("Word2 ", 200));
        var text = $"## Big Section\n\n{para1}\n\n{para2}";

        var chunks = await _sut.ChunkAsync(text, "doc.md");

        // The section header + para1 and para2 should each be their own chunk
        Assert.True(chunks.Count >= 2);
        Assert.Contains(chunks, c => c.Content.Contains("Word1"));
        Assert.Contains(chunks, c => c.Content.Contains("Word2"));
    }

    // ── Strategy 3: Sentence splitting ───────────────────────────────────────

    [Fact]
    public async Task ChunkAsync_LargeParagraphIsSentenceSplit()
    {
        // Single paragraph with many sentences, > 800 chars total
        var sentences = string.Join(" ", Enumerable.Range(1, 40).Select(i => $"Sentence {i} ends here."));
        var chunks = await _sut.ChunkAsync(sentences, "prose.txt");

        Assert.True(chunks.Count > 1, "Long paragraph must be split into multiple sentence-based chunks.");
        foreach (var c in chunks)
            Assert.True(c.Content.Length <= 900, $"Chunk too large ({c.Content.Length} chars): {c.Content[..50]}…");
    }

    // ── Edge cases ────────────────────────────────────────────────────────────

    [Fact]
    public async Task ChunkAsync_EmptyText_ReturnsEmpty()
    {
        var chunks = await _sut.ChunkAsync("", "doc.md");
        Assert.Empty(chunks);
    }

    [Fact]
    public async Task ChunkAsync_WhitespaceOnly_ReturnsEmpty()
    {
        var chunks = await _sut.ChunkAsync("   \n\n\t  ", "doc.md");
        Assert.Empty(chunks);
    }

    [Fact]
    public async Task ChunkAsync_TinyFragmentsAreDiscarded()
    {
        // Two short paragraphs under MinChunkChars (40) and one long one
        var text = "Hi\n\n" + new string('A', 100);
        var chunks = await _sut.ChunkAsync(text, "doc.txt");

        // "Hi" (2 chars) must be discarded; the long paragraph kept
        Assert.All(chunks, c => Assert.True(c.Content.Length >= 40));
    }

    [Fact]
    public async Task ChunkAsync_SequentialIndex()
    {
        var text = "## Alpha\nContent for alpha section here.\n## Beta\nContent for beta section here.\n## Gamma\nContent for gamma section here.";
        var chunks = await _sut.ChunkAsync(text, "doc.md");
        for (var i = 0; i < chunks.Count; i++)
            Assert.Equal(i, chunks[i].Index);
    }

    [Fact]
    public async Task ChunkAsync_SourceNamePropagated()
    {
        var chunks = await _sut.ChunkAsync("## Section\nContent", "my-file.md");
        Assert.All(chunks, c => Assert.Equal("my-file.md", c.Source));
    }

    [Fact]
    public async Task ChunkAsync_LargeNoHeaderParagraph_IsSentenceSplit()
    {
        // A single paragraph (no headers) that exceeds MaxParagraphChars (800)
        // should be sentence-split via the no-headers path.
        var sentences = string.Join(" ", Enumerable.Range(1, 50).Select(i => $"NoHeader sentence {i}."));
        var chunks = await _sut.ChunkAsync(sentences, "plain.txt");
        Assert.True(chunks.Count > 1, "Large single-paragraph text must be sentence-split.");
        foreach (var c in chunks)
            Assert.True(c.Content.Length <= 1000, $"Chunk too large: {c.Content.Length}");
    }

    [Fact]
    public async Task ChunkAsync_SingleSentenceParagraph_NeverSplit()
    {
        // Exactly one sentence under 800 chars — should produce exactly one chunk.
        var text = "## Intro\nThis is a single sentence that is long enough to survive the min-chunk filter.";
        var chunks = await _sut.ChunkAsync(text, "doc.md");
        Assert.Single(chunks);
    }

    [Fact]
    public async Task ChunkAsync_HeaderChunkUnderLimit_IsKeptAsIs()
    {
        // A section that is well under 2000 chars must NOT be paragraph-split.
        var text = "## Short Section\nThis section has enough content to pass the minimum chunk size filter.";
        var chunks = await _sut.ChunkAsync(text, "doc.md");
        Assert.Single(chunks);
        Assert.Contains("## Short Section", chunks[0].Content);
    }

    [Fact]
    public async Task ChunkAsync_SentenceSplitBuffer_FlushesRemainder()
    {
        // Ensure the leftover sentence buffer is flushed after the loop.
        // Construct a paragraph where the last sentence would not push the buffer over MaxParagraphChars.
        var sentences = string.Join(" ", Enumerable.Range(1, 20).Select(i => $"Buffered sentence {i}."));
        var chunks = await _sut.ChunkAsync(sentences, "buf.txt");
        Assert.NotEmpty(chunks);
        // All original sentence text must appear in the output.
        var allText = string.Concat(chunks.Select(c => c.Content));
        Assert.Contains("Buffered sentence 1.", allText);
        Assert.Contains("Buffered sentence 20.", allText);
    }
}

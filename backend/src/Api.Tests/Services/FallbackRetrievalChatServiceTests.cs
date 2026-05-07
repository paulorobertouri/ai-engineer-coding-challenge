using Api.Contracts;
using Api.Models;
using Api.Services;
using Moq;
using Xunit;

namespace Api.Tests;

public class FallbackRetrievalChatServiceTests
{
    private readonly Mock<IEmbeddingService> _mockEmbedding = new();
    private readonly Mock<IVectorStoreService> _mockVectorStore = new();
    private readonly FallbackRetrievalChatService _service;

    public FallbackRetrievalChatServiceTests()
    {
        _service = new FallbackRetrievalChatService(_mockEmbedding.Object, _mockVectorStore.Object);
        _mockEmbedding
            .Setup(e => e.EmbedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new float[1536]);
    }

    private static ChatRequest BuildRequest(string userMessage) => new()
    {
        ConversationId = "conv-1",
        Messages = [new ChatMessageDto { Role = "user", Content = userMessage }]
    };

    [Fact]
    public async Task GenerateResponseAsync_WithNoMatches_ReturnsFallbackMessage()
    {
        _mockVectorStore
            .Setup(v => v.SearchAsync(It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var response = await _service.GenerateResponseAsync(BuildRequest("tell me something"), CancellationToken.None);

        Assert.Contains("ingest", response.AssistantMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("success", response.Status);
        Assert.Empty(response.Citations);
    }

    [Fact]
    public async Task GenerateResponseAsync_WithMatches_ReturnsContextualAnswer()
    {
        var record = new VectorRecord { Id = "1", Source = "SOP.md", ChunkText = "Store opens at 9am", Embedding = new float[1536] };
        var matches = new List<VectorSearchMatch> { new() { Record = record, Score = 0.9 } };

        _mockVectorStore
            .Setup(v => v.SearchAsync(It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(matches);

        var response = await _service.GenerateResponseAsync(BuildRequest("what is the SOP?"), CancellationToken.None);

        Assert.Contains("Store opens at 9am", response.AssistantMessage);
        Assert.Single(response.Citations);
        Assert.Equal("SOP.md", response.Citations[0].Source);
    }

    [Fact]
    public async Task GenerateResponseAsync_HoursKeyword_ReturnsStoreHours()
    {
        _mockVectorStore
            .Setup(v => v.SearchAsync(It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var response = await _service.GenerateResponseAsync(BuildRequest("what are the store hours?"), CancellationToken.None);

        Assert.Contains("Monday", response.AssistantMessage);
        Assert.Contains("get_store_hours", response.ToolCalls);
    }

    [Fact]
    public async Task GenerateResponseAsync_OpenKeyword_ReturnsStoreHours()
    {
        _mockVectorStore
            .Setup(v => v.SearchAsync(It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var response = await _service.GenerateResponseAsync(BuildRequest("when does the store open?"), CancellationToken.None);

        Assert.Contains("Monday", response.AssistantMessage);
    }

    [Fact]
    public async Task GenerateResponseAsync_CloseKeyword_ReturnsStoreHours()
    {
        _mockVectorStore
            .Setup(v => v.SearchAsync(It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var response = await _service.GenerateResponseAsync(BuildRequest("when does it close?"), CancellationToken.None);

        Assert.Contains("Monday", response.AssistantMessage);
    }

    [Fact]
    public async Task GenerateResponseAsync_LongChunkText_TruncatesCitation()
    {
        var longText = new string('x', 300);
        var record = new VectorRecord { Id = "1", Source = "SOP.md", ChunkText = longText, Embedding = new float[1536] };
        var matches = new List<VectorSearchMatch> { new() { Record = record, Score = 0.9 } };

        _mockVectorStore
            .Setup(v => v.SearchAsync(It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(matches);

        var response = await _service.GenerateResponseAsync(BuildRequest("tell me"), CancellationToken.None);

        Assert.EndsWith("...", response.Citations[0].Snippet);
        Assert.True(response.Citations[0].Snippet.Length <= 203);
    }

    [Fact]
    public async Task GenerateResponseAsync_ShortChunkText_NotTruncated()
    {
        var shortText = "Short text";
        var record = new VectorRecord { Id = "1", Source = "SOP.md", ChunkText = shortText, Embedding = new float[1536] };
        var matches = new List<VectorSearchMatch> { new() { Record = record, Score = 0.9 } };

        _mockVectorStore
            .Setup(v => v.SearchAsync(It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(matches);

        var response = await _service.GenerateResponseAsync(BuildRequest("tell me"), CancellationToken.None);

        Assert.Equal(shortText, response.Citations[0].Snippet);
    }

    [Fact]
    public async Task GenerateResponseAsync_SetsConversationId()
    {
        _mockVectorStore
            .Setup(v => v.SearchAsync(It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var request = BuildRequest("hello");
        var response = await _service.GenerateResponseAsync(request, CancellationToken.None);

        Assert.Equal(request.ConversationId, response.ConversationId);
    }

    [Fact]
    public async Task GenerateResponseAsync_IsPlaceholderIsTrue()
    {
        _mockVectorStore
            .Setup(v => v.SearchAsync(It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var response = await _service.GenerateResponseAsync(BuildRequest("hi"), CancellationToken.None);

        Assert.True(response.IsPlaceholder);
    }

    [Fact]
    public async Task GenerateResponseAsync_NoUserMessage_UsesEmptyString()
    {
        _mockVectorStore
            .Setup(v => v.SearchAsync(It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var request = new ChatRequest
        {
            ConversationId = "conv-x",
            Messages = [new ChatMessageDto { Role = "assistant", Content = "I said something" }]
        };

        var response = await _service.GenerateResponseAsync(request, CancellationToken.None);

        Assert.NotNull(response.AssistantMessage);
    }

    [Fact]
    public async Task GenerateResponseAsync_LongBestChunk_TruncatesAnswerSnippet()
    {
        // BuildContextualAnswer truncates the first match's ChunkText at 500 chars
        var longChunk = new string('z', 600);
        var record = new VectorRecord { Id = "1", Source = "SOP.md", ChunkText = longChunk, Embedding = new float[1536] };
        var matches = new List<VectorSearchMatch> { new() { Record = record, Score = 0.9 } };

        _mockVectorStore
            .Setup(v => v.SearchAsync(It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(matches);

        var response = await _service.GenerateResponseAsync(BuildRequest("what is the policy?"), CancellationToken.None);

        Assert.EndsWith("...", response.AssistantMessage);
        // The truncated snippet is 500 chars + "..." = 503; plus the preamble text
        Assert.Contains("...", response.AssistantMessage);
    }

    [Fact]
    public async Task GenerateResponseAsync_ScheduleKeyword_ReturnsStoreHours()
    {
        _mockVectorStore
            .Setup(v => v.SearchAsync(It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var response = await _service.GenerateResponseAsync(BuildRequest("what is the schedule?"), CancellationToken.None);

        Assert.Contains("Monday", response.AssistantMessage);
    }

    [Fact]
    public async Task GenerateResponseAsync_NonHoursQuery_HasNoToolCalls()
    {
        var record = new VectorRecord { Id = "1", Source = "SOP.md", ChunkText = "Opening steps", Embedding = new float[1536] };
        _mockVectorStore
            .Setup(v => v.SearchAsync(It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new VectorSearchMatch { Record = record, Score = 0.9 }]);

        var response = await _service.GenerateResponseAsync(BuildRequest("tell me about the safety policy"), CancellationToken.None);

        Assert.Empty(response.ToolCalls);
    }
}

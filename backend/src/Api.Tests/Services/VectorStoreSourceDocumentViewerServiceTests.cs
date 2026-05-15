using Api.Models;
using Api.Services;
using Moq;
using Xunit;

namespace Api.Tests;

public sealed class VectorStoreSourceDocumentViewerServiceTests
{
    [Fact]
    public async Task GetDocumentAsync_ReturnsOrderedChunksForSourceAndKnowledgeBase()
    {
        var vectorStore = new Mock<IVectorStoreService>();
        vectorStore
            .Setup(service => service.LoadAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                BuildRecord("chunk-2", "Grocery_Store_SOP.md", "Second chunk", knowledgeBaseId: "default", index: 2, startLine: 21, endLine: 30),
                BuildRecord("chunk-1", "Grocery_Store_SOP.md", "First chunk", knowledgeBaseId: "default", index: 1, startLine: 10, endLine: 20),
                BuildRecord("chunk-hr", "HR.md", "Different source", knowledgeBaseId: "default", index: 1, startLine: 1, endLine: 5),
                BuildRecord("chunk-kb", "Grocery_Store_SOP.md", "Different KB", knowledgeBaseId: "operations", index: 1, startLine: 1, endLine: 5)
            ]);

        var service = new VectorStoreSourceDocumentViewerService(vectorStore.Object);

        var result = await service.GetDocumentAsync("Grocery_Store_SOP.md", "default", CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("Grocery_Store_SOP.md", result.Source);
        Assert.Equal("default", result.KnowledgeBaseId);
        Assert.Equal(2, result.Chunks.Count);
        Assert.Equal("chunk-1", result.Chunks[0].ChunkId);
        Assert.Equal("chunk-2", result.Chunks[1].ChunkId);
    }

    [Fact]
    public async Task GetDocumentAsync_ReturnsNullWhenNoMatchingChunks()
    {
        var vectorStore = new Mock<IVectorStoreService>();
        vectorStore
            .Setup(service => service.LoadAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([BuildRecord("chunk-1", "Other.md", "x", "default", 1, 1, 1)]);

        var service = new VectorStoreSourceDocumentViewerService(vectorStore.Object);

        var result = await service.GetDocumentAsync("Grocery_Store_SOP.md", "default", CancellationToken.None);

        Assert.Null(result);
    }

    private static VectorRecord BuildRecord(
        string id,
        string source,
        string chunkText,
        string knowledgeBaseId,
        int index,
        int startLine,
        int endLine)
    {
        return new VectorRecord
        {
            Id = id,
            Source = source,
            ChunkText = chunkText,
            Embedding = [],
            Metadata = new Dictionary<string, string>
            {
                [KnowledgeBaseScope.MetadataKey] = knowledgeBaseId,
                ["Index"] = index.ToString(),
                ["StartLine"] = startLine.ToString(),
                ["EndLine"] = endLine.ToString(),
                ["SectionTitle"] = "Section",
                [DocumentVersioning.DocumentVersionMetadataKey] = "sha256:test"
            }
        };
    }
}

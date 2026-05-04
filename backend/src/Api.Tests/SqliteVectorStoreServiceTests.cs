using Api.Models;
using Api.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace Api.Tests;

public class JsonVectorStoreServiceTests : IDisposable
{
    private readonly string _storeFile;
    private readonly Mock<IConfiguration> _mockConfig = new();
    private readonly Mock<IWebHostEnvironment> _mockEnv = new();

    public JsonVectorStoreServiceTests()
    {
        _storeFile = Path.Combine(Path.GetTempPath(), $"test-vector-store-{Guid.NewGuid():N}.json");
        _mockEnv.Setup(e => e.ContentRootPath).Returns(Path.GetTempPath());
        _mockConfig.Setup(c => c["Challenge:VectorStorePath"]).Returns(_storeFile);
    }

    [Fact]
    public async Task SearchAsync_ReturnsHighestScoreFirst()
    {
        // Arrange
        var service = new JsonVectorStoreService(_mockConfig.Object, _mockEnv.Object);

        var v1 = new float[1536]; v1[0] = 1.0f;
        var v2 = new float[1536]; v2[1] = 1.0f;
        var v3 = new float[1536]; v3[0] = 0.8f; v3[1] = 0.2f;

        var records = new List<VectorRecord>
        {
            new() { Id = "1", Embedding = v1, ChunkText = "Perfect match", Source = "SOP" },
            new() { Id = "2", Embedding = v2, ChunkText = "No match", Source = "SOP" },
            new() { Id = "3", Embedding = v3, ChunkText = "Partial match", Source = "SOP" }
        };

        await service.SaveAsync(records);

        var query = new float[1536]; query[0] = 1.0f;

        // Act
        var results = await service.SearchAsync(query, topK: 3);

        // Assert
        Assert.Equal(3, results.Count);
        Assert.Equal("1", results[0].Record.Id); // Score 1.0
        Assert.Equal("3", results[1].Record.Id); // Score ~0.97
        Assert.Equal("2", results[2].Record.Id); // Score 0.0
    }

    [Fact]
    public async Task LoadAsync_ReturnsEmpty_WhenFileDoesNotExist()
    {
        var service = new JsonVectorStoreService(_mockConfig.Object, _mockEnv.Object);
        var records = await service.LoadAsync();
        Assert.Empty(records);
    }

    [Fact]
    public async Task SaveAsync_ThenLoadAsync_RoundTripsRecords()
    {
        var service = new JsonVectorStoreService(_mockConfig.Object, _mockEnv.Object);

        var embedding = new float[1536]; embedding[0] = 0.5f;
        var original = new List<VectorRecord>
        {
            new() { Id = "abc", Source = "doc.md", ChunkText = "hello world", Embedding = embedding }
        };

        await service.SaveAsync(original);
        var loaded = await service.LoadAsync();

        Assert.Single(loaded);
        Assert.Equal("abc", loaded[0].Id);
        Assert.Equal("hello world", loaded[0].ChunkText);
        Assert.Equal(0.5f, loaded[0].Embedding[0]);
    }

    public void Dispose()
    {
        if (File.Exists(_storeFile)) File.Delete(_storeFile);
    }
}
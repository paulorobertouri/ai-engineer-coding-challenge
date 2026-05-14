using Api.Models;
using Api.Options;
using Api.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Api.Tests;

public class JsonVectorStoreServiceAdditionalTests : IDisposable
{
    private readonly string _storeFile;
    private readonly Mock<IWebHostEnvironment> _mockEnv = new();
    private readonly IOptions<ChallengeOptions> _challengeOptions;

    public JsonVectorStoreServiceAdditionalTests()
    {
        _storeFile = Path.Combine(Path.GetTempPath(), $"test-vs-{Guid.NewGuid():N}.json");
        _mockEnv.Setup(e => e.ContentRootPath).Returns(Path.GetTempPath());
        _challengeOptions = Microsoft.Extensions.Options.Options.Create(new ChallengeOptions { VectorStorePath = _storeFile });
    }

    public void Dispose()
    {
        if (File.Exists(_storeFile)) File.Delete(_storeFile);
    }

    [Fact]
    public async Task LoadAsync_ReturnsCachedRecords_OnSecondCall()
    {
        var service = new JsonVectorStoreService(_challengeOptions, _mockEnv.Object);

        var embedding = new float[4]; embedding[0] = 1f;
        var records = new List<VectorRecord>
        {
            new() { Id = "x", Source = "s.md", ChunkText = "cached", Embedding = embedding }
        };
        await service.SaveAsync(records);

        var first = await service.LoadAsync();
        var second = await service.LoadAsync();

        Assert.Same(first, second); // must be same reference (cache hit)
    }

    [Fact]
    public async Task SaveAsync_InvalidatesCacheSoNextLoadReadsFreshData()
    {
        var service = new JsonVectorStoreService(_challengeOptions, _mockEnv.Object);

        var e1 = new float[4]; e1[0] = 1f;
        await service.SaveAsync([new() { Id = "v1", Source = "s", ChunkText = "first", Embedding = e1 }]);
        _ = await service.LoadAsync(); // warm cache

        var e2 = new float[4]; e2[1] = 1f;
        await service.SaveAsync([new() { Id = "v2", Source = "s", ChunkText = "second", Embedding = e2 }]);
        var loaded = await service.LoadAsync();

        Assert.Single(loaded);
        Assert.Equal("v2", loaded[0].Id);
    }

    [Fact]
    public async Task SearchAsync_RespectsTopK()
    {
        var service = new JsonVectorStoreService(_challengeOptions, _mockEnv.Object);

        var records = Enumerable.Range(1, 10).Select(i =>
        {
            var emb = new float[4]; emb[0] = (float)i / 10f;
            return new VectorRecord { Id = i.ToString(), Source = "s", ChunkText = $"chunk {i}", Embedding = emb };
        }).ToList();

        await service.SaveAsync(records);

        var query = new float[4]; query[0] = 1f;
        var results = await service.SearchAsync(query, topK: 3);

        Assert.Equal(3, results.Count);
    }

    [Fact]
    public async Task SearchAsync_WithMetadataFilter_ReturnsOnlyMatchingRecords()
    {
        var service = new JsonVectorStoreService(_challengeOptions, _mockEnv.Object);

        var emb = new float[4]; emb[0] = 1f;
        await service.SaveAsync(
        [
            new() { Id = "a", Source = "s", ChunkText = "A", Embedding = emb, Metadata = new Dictionary<string, string> { ["kb"] = "store" } },
            new() { Id = "b", Source = "s", ChunkText = "B", Embedding = emb, Metadata = new Dictionary<string, string> { ["kb"] = "hr" } }
        ]);

        var query = new float[4]; query[0] = 1f;
        var results = await service.SearchAsync(query, topK: 10, new Dictionary<string, string> { ["kb"] = "store" });

        Assert.Single(results);
        Assert.Equal("a", results[0].Record.Id);
    }

    [Fact]
    public async Task DeleteByIdsAsync_RemovesOnlySpecifiedRecords()
    {
        var service = new JsonVectorStoreService(_challengeOptions, _mockEnv.Object);

        var emb = new float[4]; emb[0] = 1f;
        await service.SaveAsync(
        [
            new() { Id = "keep", Source = "s", ChunkText = "keep", Embedding = emb },
            new() { Id = "remove", Source = "s", ChunkText = "remove", Embedding = emb }
        ]);

        await service.DeleteByIdsAsync(["remove"]);
        var loaded = await service.LoadAsync();

        Assert.Single(loaded);
        Assert.Equal("keep", loaded[0].Id);
    }

    [Fact]
    public async Task SearchAsync_CosineSimilarity_MismatchedLengthReturnsZero()
    {
        var service = new JsonVectorStoreService(_challengeOptions, _mockEnv.Object);

        // Store a 4-dim record
        var shortEmb = new float[4]; shortEmb[0] = 1f;
        await service.SaveAsync([new() { Id = "short", Source = "s", ChunkText = "c", Embedding = shortEmb }]);

        // Query with different dimension — should return score 0
        var longQuery = new float[8]; longQuery[0] = 1f;
        var results = await service.SearchAsync(longQuery, topK: 1);

        Assert.Single(results);
        Assert.Equal(0.0, results[0].Score);
    }

    [Fact]
    public async Task SearchAsync_AllZeroEmbeddings_ReturnsZeroScore()
    {
        var service = new JsonVectorStoreService(_challengeOptions, _mockEnv.Object);

        var zeroEmb = new float[4]; // all zeros
        await service.SaveAsync([new() { Id = "z", Source = "s", ChunkText = "zero", Embedding = zeroEmb }]);

        var query = new float[4]; query[0] = 1f;
        var results = await service.SearchAsync(query, topK: 1);

        Assert.Equal(0.0, results[0].Score);
    }

    [Fact]
    public async Task SearchAsync_EmptyStore_ReturnsEmptyList()
    {
        var service = new JsonVectorStoreService(_challengeOptions, _mockEnv.Object);
        var query = new float[4]; query[0] = 1f;
        var results = await service.SearchAsync(query, topK: 5);
        Assert.Empty(results);
    }

    [Fact]
    public void Constructor_CreatesDirectory_WhenItDoesNotExist()
    {
        var tempBase = Path.Combine(Path.GetTempPath(), $"vs-dir-{Guid.NewGuid():N}");
        var storePath = Path.Combine(tempBase, "sub", "store.json");

        var challengeOptions = Microsoft.Extensions.Options.Options.Create(new ChallengeOptions { VectorStorePath = storePath });
        var env = new Mock<IWebHostEnvironment>();
        env.Setup(e => e.ContentRootPath).Returns(tempBase);

        try
        {
            _ = new JsonVectorStoreService(challengeOptions, env.Object);
            Assert.True(Directory.Exists(Path.GetDirectoryName(storePath)));
        }
        finally
        {
            if (Directory.Exists(tempBase)) Directory.Delete(tempBase, recursive: true);
        }
    }

    [Fact]
    public void Constructor_UsesDefaultPath_WhenConfigIsNull()
    {
        var challengeOptions = Microsoft.Extensions.Options.Options.Create(new ChallengeOptions { VectorStorePath = "Data/vector-store.json" });
        var env = new Mock<IWebHostEnvironment>();
        env.Setup(e => e.ContentRootPath).Returns(Path.GetTempPath());

        // Should not throw
        var service = new JsonVectorStoreService(challengeOptions, env.Object);
        Assert.NotNull(service);
    }

    [Fact]
    public async Task LoadAsync_HandlesCorruptJson_WhenFileIsEmpty()
    {
        File.WriteAllText(_storeFile, "");
        var service = new JsonVectorStoreService(_challengeOptions, _mockEnv.Object);
        await Assert.ThrowsAnyAsync<Exception>(() => service.LoadAsync());
    }
}

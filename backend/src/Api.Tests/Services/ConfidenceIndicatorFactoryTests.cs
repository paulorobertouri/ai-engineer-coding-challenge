using Api.Contracts;
using Api.Models;
using Api.Services;
using Xunit;

namespace Api.Tests;

public class ConfidenceIndicatorFactoryTests
{
    [Fact]
    public void Create_NoMatches_ReturnsNotFound()
    {
        var result = ConfidenceIndicatorFactory.Create([], []);

        Assert.Equal(ConfidenceIndicatorDto.NotFound, result.Level);
        Assert.Equal(0, result.EvidenceCoverage);
    }

    [Fact]
    public void Create_StrongMatchesWithFullCoverage_ReturnsHigh()
    {
        var matches = new[]
        {
            Match("chunk-1", 0.92),
            Match("chunk-2", 0.85)
        };

        var result = ConfidenceIndicatorFactory.Create(matches, ["chunk-1", "chunk-2"]);

        Assert.Equal(ConfidenceIndicatorDto.High, result.Level);
        Assert.Equal(1, result.EvidenceCoverage);
    }

    [Fact]
    public void Create_WeakMatches_ReturnsLow()
    {
        var matches = new[]
        {
            Match("chunk-1", 0.52),
            Match("chunk-2", 0.49)
        };

        var result = ConfidenceIndicatorFactory.Create(matches, ["chunk-1"]);

        Assert.Equal(ConfidenceIndicatorDto.Low, result.Level);
        Assert.Equal(0.5, result.EvidenceCoverage);
    }

    [Fact]
    public void Create_ModerateMatchesWithPartialCoverage_ReturnsMedium()
    {
        var matches = new[]
        {
            Match("chunk-1", 0.72),
            Match("chunk-2", 0.64),
            Match("chunk-3", 0.60)
        };

        var result = ConfidenceIndicatorFactory.Create(matches, ["chunk-1", "chunk-2"]);

        Assert.Equal(ConfidenceIndicatorDto.Medium, result.Level);
        Assert.Equal(0.667, result.EvidenceCoverage);
    }

    private static VectorSearchMatch Match(string chunkId, double score) =>
        new()
        {
            Score = score,
            Record = new VectorRecord
            {
                Id = chunkId,
                Source = "SOP.md",
                ChunkText = "Example"
            }
        };
}

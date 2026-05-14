using Api.Models;
using Api.Services;
using Xunit;

namespace Api.Tests;

public class LexicalRetrievalRerankerTests
{
    private readonly LexicalRetrievalReranker _reranker = new();

    [Fact]
    public void Rerank_PrioritizesLexicalOverlap()
    {
        var candidates = new List<VectorSearchMatch>
        {
            Match("a", 0.81, "Store opening checklist includes alarms and registers."),
            Match("b", 0.78, "Employee discount policy and break scheduling guidance.")
        };

        var result = _reranker.Rerank("opening checklist", candidates, 2);

        Assert.Equal("a", result[0].Record.Id);
    }

    [Fact]
    public void Rerank_RespectsTakeLimit()
    {
        var candidates = new List<VectorSearchMatch>
        {
            Match("a", 0.91, "a"),
            Match("b", 0.90, "b"),
            Match("c", 0.89, "c")
        };

        var result = _reranker.Rerank("anything", candidates, 2);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void Rerank_SingleCandidate_ReturnsCandidate()
    {
        var candidates = new List<VectorSearchMatch>
        {
            Match("only", 0.63, "single candidate text")
        };

        var result = _reranker.Rerank("single", candidates, 3);

        Assert.Single(result);
        Assert.Equal("only", result[0].Record.Id);
    }

    private static VectorSearchMatch Match(string id, double score, string text) =>
        new()
        {
            Score = score,
            Record = new VectorRecord
            {
                Id = id,
                Source = "SOP.md",
                ChunkText = text
            }
        };
}

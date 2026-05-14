using Api.Models;
using System.Text.RegularExpressions;

namespace Api.Services;

public sealed class LexicalRetrievalReranker : IRetrievalReranker
{
    private static readonly Regex TokenRegex = new("[a-z0-9]+", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public string Name => "lexical_overlap";

    public IReadOnlyList<VectorSearchMatch> Rerank(
        string query,
        IReadOnlyList<VectorSearchMatch> candidates,
        int take)
    {
        if (candidates.Count <= 1)
        {
            return candidates.Take(Math.Max(1, take)).ToList();
        }

        var queryTokens = Tokenize(query);

        return candidates
            .Select(candidate =>
            {
                var lexicalScore = ComputeLexicalScore(queryTokens, candidate.Record.ChunkText);
                // Keep semantic score dominant while using lexical overlap as reranking signal.
                var combinedScore = (candidate.Score * 0.7) + (lexicalScore * 0.3);

                return new VectorSearchMatch
                {
                    Record = candidate.Record,
                    Score = Math.Round(combinedScore, 4)
                };
            })
            .OrderByDescending(match => match.Score)
            .Take(Math.Max(1, take))
            .ToList();
    }

    private static HashSet<string> Tokenize(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        return TokenRegex
            .Matches(text.ToLowerInvariant())
            .Select(match => match.Value)
            .Where(token => token.Length > 2)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static double ComputeLexicalScore(IReadOnlySet<string> queryTokens, string chunkText)
    {
        if (queryTokens.Count == 0)
        {
            return 0;
        }

        var chunkTokens = Tokenize(chunkText);
        if (chunkTokens.Count == 0)
        {
            return 0;
        }

        var overlap = queryTokens.Count(chunkTokens.Contains);
        return (double)overlap / queryTokens.Count;
    }
}

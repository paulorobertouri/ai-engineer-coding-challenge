using Api.Contracts;
using Api.Models;

namespace Api.Services;

public static class ConfidenceIndicatorFactory
{
    public static ConfidenceIndicatorDto Create(
        IReadOnlyCollection<VectorSearchMatch> matches,
        IReadOnlyCollection<string> citedChunkIds)
    {
        if (matches.Count == 0)
        {
            return new ConfidenceIndicatorDto
            {
                Level = ConfidenceIndicatorDto.NotFound,
                EvidenceCoverage = 0
            };
        }

        var matchedChunkIds = matches
            .Select(match => match.Record.Id)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.Ordinal);

        var citedMatchCount = citedChunkIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .Count(matchedChunkIds.Contains);

        var evidenceCoverage = Math.Round((double)citedMatchCount / matchedChunkIds.Count, 3);
        var bestScore = matches.Max(match => match.Score);
        var averageScore = matches.Average(match => match.Score);

        var level = ResolveLevel(bestScore, averageScore, evidenceCoverage);

        return new ConfidenceIndicatorDto
        {
            Level = level,
            EvidenceCoverage = evidenceCoverage
        };
    }

    private static string ResolveLevel(double bestScore, double averageScore, double evidenceCoverage)
    {
        if (bestScore >= 0.85 && averageScore >= 0.75 && evidenceCoverage >= 0.6)
        {
            return ConfidenceIndicatorDto.High;
        }

        if (bestScore >= 0.65 && averageScore >= 0.55 && evidenceCoverage >= 0.4)
        {
            return ConfidenceIndicatorDto.Medium;
        }

        return ConfidenceIndicatorDto.Low;
    }
}

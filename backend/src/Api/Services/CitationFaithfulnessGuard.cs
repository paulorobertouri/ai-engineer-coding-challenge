using Api.Models;
using System.Text.RegularExpressions;

namespace Api.Services;

internal static class CitationFaithfulnessGuard
{
    private static readonly Regex SentenceSplitRegex = new(@"[.!?]+", RegexOptions.Compiled);
    private static readonly Regex TokenRegex = new(@"[a-zA-Z]{4,}", RegexOptions.Compiled);

    private static readonly string[] BenignLeadIns =
    [
        "based on the sop",
        "according to the sop",
        "conforme o sop",
        "conformement au sop",
        "d'apres le sop",
        "basado en el sop"
    ];

    public static bool HasUnsupportedClaims(string answerText, IReadOnlyList<VectorSearchMatch> matches)
    {
        if (string.IsNullOrWhiteSpace(answerText) || matches.Count == 0)
        {
            return false;
        }

        var citationTokens = matches
            .SelectMany(match => ExtractTokens(match.Record.ChunkText))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (citationTokens.Count == 0)
        {
            return false;
        }

        var sentences = SentenceSplitRegex
            .Split(answerText)
            .Select(sentence => sentence.Trim())
            .Where(sentence => sentence.Length > 0)
            .ToList();

        foreach (var sentence in sentences)
        {
            if (IsBenignLeadIn(sentence))
            {
                continue;
            }

            var sentenceTokens = ExtractTokens(sentence).ToList();
            if (sentenceTokens.Count < 5)
            {
                continue;
            }

            var overlapCount = sentenceTokens.Count(token => citationTokens.Contains(token));
            if (overlapCount == 0)
            {
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<string> ExtractTokens(string text)
    {
        foreach (Match match in TokenRegex.Matches(text))
        {
            yield return match.Value;
        }
    }

    private static bool IsBenignLeadIn(string sentence)
    {
        var normalized = sentence.Trim().ToLowerInvariant();
        return BenignLeadIns.Any(leadIn => normalized.StartsWith(leadIn, StringComparison.Ordinal));
    }
}
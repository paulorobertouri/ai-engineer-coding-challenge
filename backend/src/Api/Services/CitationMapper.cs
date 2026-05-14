using Api.Contracts;
using Api.Models;

namespace Api.Services;

internal static class CitationMapper
{
    public static CitationDto FromMatch(VectorSearchMatch match)
    {
        return new CitationDto
        {
            ChunkId = match.Record.Id,
            KnowledgeBaseId = KnowledgeBaseScope.GetRecordKnowledgeBaseId(match.Record),
            Source = match.Record.Source,
            SectionTitle = TryGetMetadata(match.Record, "SectionTitle") ?? ExtractSectionTitle(match.Record.ChunkText),
            Snippet = BuildSnippet(match.Record.ChunkText),
            Score = Math.Round(match.Score, 4),
            StartLine = TryGetIntMetadata(match.Record, "StartLine"),
            EndLine = TryGetIntMetadata(match.Record, "EndLine")
        };
    }

    private static string BuildSnippet(string chunkText) =>
        chunkText.Length > 200 ? chunkText[..200] + "..." : chunkText;

    private static string ExtractSectionTitle(string chunkText)
    {
        var firstLine = chunkText
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault()?
            .Trim();

        if (string.IsNullOrWhiteSpace(firstLine))
        {
            return string.Empty;
        }

        return firstLine.StartsWith('#')
            ? firstLine.TrimStart('#').Trim()
            : string.Empty;
    }

    private static int? TryGetIntMetadata(VectorRecord record, string key)
    {
        if (!record.Metadata.TryGetValue(key, out var value))
        {
            return null;
        }

        return int.TryParse(value, out var parsed) ? parsed : null;
    }

    private static string? TryGetMetadata(VectorRecord record, string key)
    {
        if (!record.Metadata.TryGetValue(key, out var value))
        {
            return null;
        }

        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}

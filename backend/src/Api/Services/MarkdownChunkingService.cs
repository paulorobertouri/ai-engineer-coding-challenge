using System.Text.RegularExpressions;
using Api.Models;

namespace Api.Services;

public sealed partial class MarkdownChunkingService : IChunkingService
{
    public Task<IReadOnlyList<TextChunk>> ChunkAsync(string sourceText, string sourceName, CancellationToken cancellationToken = default)
    {
        var chunks = new List<TextChunk>();

        // Split on level-1 or level-2 markdown headers so that a top-level title
        // ("# Title") is treated as its own chunk rather than being prepended to the
        // first level-2 section.
        var sections = MyRegex().Split(sourceText);

        int index = 0;
        int searchStart = 0;
        foreach (var section in sections)
        {
            var trimmed = section.Trim();
            if (string.IsNullOrWhiteSpace(trimmed)) continue;

            var sectionStart = sourceText.IndexOf(section, searchStart, StringComparison.Ordinal);
            if (sectionStart < 0)
            {
                sectionStart = searchStart;
            }

            var trimmedStartInSection = section.IndexOf(trimmed, StringComparison.Ordinal);
            var chunkStartIndex = sectionStart + Math.Max(0, trimmedStartInSection);
            var chunkEndIndex = chunkStartIndex + trimmed.Length - 1;
            var startLine = GetLineNumber(sourceText, chunkStartIndex);
            var endLine = GetLineNumber(sourceText, chunkEndIndex);

            searchStart = Math.Min(sourceText.Length, sectionStart + section.Length);

            chunks.Add(new TextChunk
            {
                Id = Guid.NewGuid().ToString("N"),
                Source = sourceName,
                Index = index++,
                StartLine = startLine,
                EndLine = endLine,
                SectionTitle = ExtractSectionTitle(trimmed),
                Content = trimmed
            });
        }

        return Task.FromResult<IReadOnlyList<TextChunk>>(chunks);
    }

    [GeneratedRegex(@"(?m)^(?=#{1,2}\s)")]
    private static partial Regex MyRegex();

    private static int GetLineNumber(string text, int charIndex)
    {
        if (text.Length == 0)
        {
            return 1;
        }

        var boundedIndex = Math.Clamp(charIndex, 0, text.Length - 1);
        var line = 1;

        for (var i = 0; i < boundedIndex; i++)
        {
            if (text[i] == '\n')
            {
                line++;
            }
        }

        return line;
    }

    private static string ExtractSectionTitle(string chunkContent)
    {
        var firstLine = chunkContent
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault()?
            .Trim();

        if (string.IsNullOrWhiteSpace(firstLine))
        {
            return string.Empty;
        }

        if (firstLine.StartsWith('#'))
        {
            return firstLine.TrimStart('#').Trim();
        }

        return string.Empty;
    }
}
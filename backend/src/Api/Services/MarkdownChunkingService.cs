using System.Text.RegularExpressions;
using Api.Models;

namespace Api.Services;

public sealed class MarkdownChunkingService : IChunkingService
{
    public Task<IReadOnlyList<TextChunk>> ChunkAsync(string sourceText, string sourceName, CancellationToken cancellationToken = default)
    {
        var chunks = new List<TextChunk>();

        // Split by markdown headers (Level 2)
        // This pattern matches "## " at the start of a line and splits the text.
        // We use lookbehind/lookahead or just split and then rejoin headers if needed.
        // For simplicity, let's split by double newline + header or just headers.

        var sections = Regex.Split(sourceText, @"(?m)^(?=##\s)");

        int index = 0;
        foreach (var section in sections)
        {
            var trimmed = section.Trim();
            if (string.IsNullOrWhiteSpace(trimmed)) continue;

            chunks.Add(new TextChunk
            {
                Id = Guid.NewGuid().ToString("N"),
                Source = sourceName,
                Index = index++,
                Content = trimmed
            });
        }

        return Task.FromResult<IReadOnlyList<TextChunk>>(chunks);
    }
}
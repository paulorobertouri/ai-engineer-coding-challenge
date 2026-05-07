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

    [GeneratedRegex(@"(?m)^(?=#{1,2}\s)")]
    private static partial Regex MyRegex();
}
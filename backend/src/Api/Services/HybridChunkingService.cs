using System.Text;
using System.Text.RegularExpressions;
using Api.Models;

namespace Api.Services;

/// <summary>
/// Hierarchical chunking pipeline that applies three strategies in order:
///   1. <b>Header</b>: split the document on any markdown heading line (# through ####).
///   2. <b>Paragraph</b>: for header sections longer than <see cref="MaxHeaderChunkChars"/>,
///      split further on blank-line paragraph boundaries.
///   3. <b>Sentence</b>: for paragraphs longer than <see cref="MaxParagraphChars"/>,
///      split further on sentence-ending punctuation and re-group into size-bounded windows.
///
/// Documents that contain no headings skip step 1 and go straight to paragraph splitting.
/// Fragments shorter than <see cref="MinChunkChars"/> are always discarded.
/// </summary>
public sealed partial class HybridChunkingService : IChunkingService
{
    private const int MaxHeaderChunkChars = 2000;
    private const int MaxParagraphChars = 800;
    private const int MinChunkChars = 40;

    public Task<IReadOnlyList<TextChunk>> ChunkAsync(
        string sourceText, string sourceName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sourceText))
            return Task.FromResult<IReadOnlyList<TextChunk>>([]);

        var headerSections = SplitByHeaders(sourceText);

        var rawChunks = new List<string>();

        // If the document has no headings (or just one blob), skip straight to paragraphs.
        if (headerSections.Count <= 1)
        {
            foreach (var para in SplitByParagraphs(sourceText))
            {
                if (para.Length <= MaxParagraphChars)
                    rawChunks.Add(para);
                else
                    rawChunks.AddRange(SplitBySentences(para));
            }
        }
        else
        {
            foreach (var section in headerSections)
            {
                if (section.Length <= MaxHeaderChunkChars)
                {
                    rawChunks.Add(section);
                }
                else
                {
                    // Strategy 2: paragraph-split the oversized section.
                    var paragraphs = SplitByParagraphs(section);
                    foreach (var para in paragraphs)
                    {
                        if (para.Length <= MaxParagraphChars)
                        {
                            rawChunks.Add(para);
                        }
                        else
                        {
                            // Strategy 3: sentence-split the oversized paragraph.
                            rawChunks.AddRange(SplitBySentences(para));
                        }
                    }
                }
            }
        }

        var result = rawChunks
            .Select(c => c.Trim())
            .Where(c => c.Length >= MinChunkChars)
            .Select((c, idx) => new TextChunk
            {
                Id = Guid.NewGuid().ToString("N"),
                Source = sourceName,
                Index = idx,
                Content = c,
            })
            .ToList();

        return Task.FromResult<IReadOnlyList<TextChunk>>(result);
    }

    // ── Strategy 1: Header splitting ─────────────────────────────────────────

    private static List<string> SplitByHeaders(string text)
    {
        return HeaderRegex()
            .Split(text)
            .Select(s => s.Trim())
            .Where(s => s.Length > 0)
            .ToList();
    }

    // ── Strategy 2: Paragraph splitting ──────────────────────────────────────

    private static List<string> SplitByParagraphs(string text)
    {
        return BlankLineRegex()
            .Split(text)
            .Select(p => p.Trim())
            .Where(p => p.Length > 0)
            .ToList();
    }

    // ── Strategy 3: Sentence splitting ───────────────────────────────────────

    private static List<string> SplitBySentences(string text)
    {
        var sentences = SentenceBoundaryRegex()
            .Split(text)
            .Select(s => s.Trim())
            .Where(s => s.Length > 0)
            .ToList();

        if (sentences.Count == 0)
            return [text.Trim()];

        // Re-group sentences into windows that stay under MaxParagraphChars.
        var groups = new List<string>();
        var buffer = new StringBuilder();

        foreach (var sentence in sentences)
        {
            if (buffer.Length > 0 && buffer.Length + sentence.Length + 1 > MaxParagraphChars)
            {
                groups.Add(buffer.ToString().Trim());
                buffer.Clear();
            }

            if (buffer.Length > 0) buffer.Append(' ');
            buffer.Append(sentence);
        }

        if (buffer.Length > 0)
            groups.Add(buffer.ToString().Trim());

        return groups;
    }

    // ── Compiled regexes ─────────────────────────────────────────────────────

    // Splits before any markdown heading line: # through ####
    [GeneratedRegex(@"(?m)^(?=#{1,4}\s)")]
    private static partial Regex HeaderRegex();

    // Splits on two or more consecutive newlines (blank-line paragraph break)
    [GeneratedRegex(@"\n{2,}")]
    private static partial Regex BlankLineRegex();

    // Splits after sentence-ending punctuation (. ! ?) followed by whitespace
    [GeneratedRegex(@"(?<=[.!?])\s+")]
    private static partial Regex SentenceBoundaryRegex();
}

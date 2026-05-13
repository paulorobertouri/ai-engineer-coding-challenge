namespace Api.Models;

public sealed class TextChunk
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    public string Source { get; init; } = string.Empty;

    public int Index { get; init; }

    public int? StartLine { get; init; }

    public int? EndLine { get; init; }

    public string SectionTitle { get; init; } = string.Empty;

    public string Content { get; init; } = string.Empty;
}
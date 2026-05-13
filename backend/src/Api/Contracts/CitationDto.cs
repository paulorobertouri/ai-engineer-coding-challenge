namespace Api.Contracts;

public sealed class CitationDto
{
    public string ChunkId { get; init; } = string.Empty;

    public string Source { get; init; } = string.Empty;

    public string SectionTitle { get; init; } = string.Empty;

    public string Snippet { get; init; } = string.Empty;

    public double? Score { get; init; }

    public int? StartLine { get; init; }

    public int? EndLine { get; init; }
}
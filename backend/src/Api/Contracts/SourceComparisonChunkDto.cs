namespace Api.Contracts;

public sealed class SourceComparisonChunkDto
{
    public int? Index { get; init; }

    public string SectionTitle { get; init; } = string.Empty;

    public int? StartLine { get; init; }

    public int? EndLine { get; init; }

    public string? IngestedChunkId { get; init; }

    public string? CurrentChunkId { get; init; }

    public string ChangeType { get; init; } = "unchanged";

    public bool IsImpactedCitation { get; init; }

    public string? IngestedContent { get; init; }

    public string? CurrentContent { get; init; }
}
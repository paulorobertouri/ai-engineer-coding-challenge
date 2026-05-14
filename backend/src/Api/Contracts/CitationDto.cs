namespace Api.Contracts;

public sealed class CitationDto
{
    public string ChunkId { get; init; } = string.Empty;

    public string KnowledgeBaseId { get; init; } = string.Empty;

    public string DocumentVersion { get; init; } = string.Empty;

    public string SourceChecksum { get; init; } = string.Empty;

    public DateTimeOffset? IngestedAtUtc { get; init; }

    public string Source { get; init; } = string.Empty;

    public string SectionTitle { get; init; } = string.Empty;

    public string Snippet { get; init; } = string.Empty;

    public double? Score { get; init; }

    public int? StartLine { get; init; }

    public int? EndLine { get; init; }
}
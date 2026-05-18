namespace Api.Contracts;

public sealed class SourceListItemDto
{
    public string Source { get; init; } = string.Empty;

    public string KnowledgeBaseId { get; init; } = string.Empty;

    public int ChunkCount { get; init; }

    public string? DocumentVersion { get; init; }

    public string? SourceChecksum { get; init; }

    public DateTimeOffset? IngestedAtUtc { get; init; }
}
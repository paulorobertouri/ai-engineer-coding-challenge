namespace Api.Contracts;

public sealed class SourceComparisonResponse
{
    public string Source { get; init; } = string.Empty;

    public string KnowledgeBaseId { get; init; } = string.Empty;

    public string? IngestedDocumentVersion { get; init; }

    public string CurrentDocumentVersion { get; init; } = string.Empty;

    public int ChangedChunkCount { get; init; }

    public int TotalComparedChunks { get; init; }

    public IReadOnlyList<SourceComparisonChunkDto> Chunks { get; init; } = [];
}
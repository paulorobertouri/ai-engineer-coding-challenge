namespace Api.Contracts;

public sealed class SourceQualityReportResponse
{
    public string Source { get; init; } = string.Empty;

    public string KnowledgeBaseId { get; init; } = string.Empty;

    public int TotalChunks { get; init; }

    public int DuplicateSectionCount { get; init; }

    public int WeakExtractionZoneCount { get; init; }

    public IReadOnlyList<SourceQualityOutlierDto> ShortestChunks { get; init; } = [];

    public IReadOnlyList<SourceQualityOutlierDto> LongestChunks { get; init; } = [];
}
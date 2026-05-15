namespace Api.Contracts;

public sealed record IngestionAuditRecord
{
    public DateTimeOffset TimestampUtc { get; init; }

    public string Outcome { get; init; } = string.Empty;

    public string Action { get; init; } = string.Empty;

    public string KnowledgeBaseId { get; init; } = string.Empty;

    public string SourceName { get; init; } = string.Empty;

    public string? SourceChecksum { get; init; }

    public string? DocumentVersion { get; init; }

    public int? ChunkCount { get; init; }

    public int? RecordsPersisted { get; init; }

    public string? SafeSummary { get; init; }

    public string? TriggeredBy { get; init; }
}

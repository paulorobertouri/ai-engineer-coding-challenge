namespace Api.Contracts;

public sealed class IngestResponse
{
    public bool Accepted { get; init; }

    public string Message { get; init; } = string.Empty;

    public string SourcePath { get; init; } = string.Empty;

    public int ChunksCreated { get; init; }

    public int RecordsPersisted { get; init; }

    public string VectorStorePath { get; init; } = string.Empty;

    public string KnowledgeBaseId { get; init; } = string.Empty;

    public string DocumentVersion { get; init; } = string.Empty;

    public string SourceChecksum { get; init; } = string.Empty;

    public DateTimeOffset IngestedAtUtc { get; init; }

    public bool IsPlaceholder { get; init; }

    public Guid? JobId { get; init; }

    public string? JobStatus { get; init; }

    public string? JobStatusUrl { get; init; }
}
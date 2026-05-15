namespace Api.Contracts;

public sealed class IngestPreviewResponse
{
    public bool Accepted { get; init; }

    public string Message { get; init; } = string.Empty;

    public string SourceName { get; init; } = string.Empty;

    public int ChunkCount { get; init; }

    public IReadOnlyList<IngestPreviewChunk> Chunks { get; init; } = [];
}

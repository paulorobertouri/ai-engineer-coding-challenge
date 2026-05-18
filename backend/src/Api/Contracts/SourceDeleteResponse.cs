namespace Api.Contracts;

public sealed class SourceDeleteResponse
{
    public string Source { get; init; } = string.Empty;

    public string KnowledgeBaseId { get; init; } = string.Empty;

    public int RemovedChunks { get; init; }

    public string Message { get; init; } = string.Empty;
}
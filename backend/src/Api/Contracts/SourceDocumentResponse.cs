namespace Api.Contracts;

public sealed class SourceDocumentResponse
{
    public string Source { get; init; } = string.Empty;

    public string KnowledgeBaseId { get; init; } = string.Empty;

    public string? DocumentVersion { get; init; }

    public IReadOnlyList<SourceDocumentChunkDto> Chunks { get; init; } = [];
}

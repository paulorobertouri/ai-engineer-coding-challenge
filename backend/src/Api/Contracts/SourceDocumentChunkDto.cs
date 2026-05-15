namespace Api.Contracts;

public sealed class SourceDocumentChunkDto
{
    public string ChunkId { get; init; } = string.Empty;

    public string SectionTitle { get; init; } = string.Empty;

    public string Content { get; init; } = string.Empty;

    public int? StartLine { get; init; }

    public int? EndLine { get; init; }

    public int? Index { get; init; }
}

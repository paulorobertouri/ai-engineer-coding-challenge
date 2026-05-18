namespace Api.Contracts;

public sealed class SourceQualityOutlierDto
{
    public string ChunkId { get; init; } = string.Empty;

    public string SectionTitle { get; init; } = string.Empty;

    public int CharacterCount { get; init; }

    public int? StartLine { get; init; }

    public int? EndLine { get; init; }
}
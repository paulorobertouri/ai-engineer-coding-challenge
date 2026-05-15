namespace Api.Contracts;

public sealed class IngestPreviewChunk
{
    public string Id { get; init; } = string.Empty;

    public string SectionTitle { get; init; } = string.Empty;

    public int CharacterCount { get; init; }

    public string SampleText { get; init; } = string.Empty;
}

namespace Api.Contracts;

public sealed class StructuredAnswerDto
{
    public const string NotFoundReason = "not_found";

    public string AnswerText { get; init; } = string.Empty;

    public List<string> CitedChunkIds { get; init; } = [];

    public string? RefusalReason { get; init; }

    public List<string> FollowUpSuggestions { get; init; } = [];
}

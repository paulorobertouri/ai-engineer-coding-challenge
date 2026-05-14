namespace Api.Contracts;

public sealed class ChatResponse
{
    public string ConversationId { get; init; } = string.Empty;

    public string AssistantMessage { get; init; } = string.Empty;

    public string Status { get; init; } = "placeholder";

    public bool IsPlaceholder { get; init; }

    public List<string> ToolCalls { get; init; } = [];

    public List<CitationDto> Citations { get; init; } = [];

    public StructuredAnswerDto StructuredOutput { get; init; } = new();
}
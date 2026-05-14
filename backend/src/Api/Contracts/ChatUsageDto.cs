namespace Api.Contracts;

public sealed class ChatUsageDto
{
    public string Model { get; init; } = string.Empty;

    public string Source { get; init; } = string.Empty;

    public bool IsEstimated { get; init; }

    public int PromptTokens { get; init; }

    public int CompletionTokens { get; init; }

    public int EmbeddingTokens { get; init; }

    public int TotalTokens { get; init; }

    public decimal EstimatedCostUsd { get; init; }
}

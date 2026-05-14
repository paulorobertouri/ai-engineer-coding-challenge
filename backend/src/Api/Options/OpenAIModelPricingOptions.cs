using System.ComponentModel.DataAnnotations;

namespace Api.Options;

public sealed class OpenAIModelPricingOptions
{
    [Required]
    [MinLength(1)]
    public string Model { get; init; } = string.Empty;

    [Range(0, double.MaxValue)]
    public decimal PromptTokensPerMillionUsd { get; init; }

    [Range(0, double.MaxValue)]
    public decimal CompletionTokensPerMillionUsd { get; init; }

    [Range(0, double.MaxValue)]
    public decimal EmbeddingTokensPerMillionUsd { get; init; }
}

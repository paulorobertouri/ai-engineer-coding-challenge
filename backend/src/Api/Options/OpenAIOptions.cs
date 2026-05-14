using System.ComponentModel.DataAnnotations;

namespace Api.Options;

public sealed class OpenAIOptions
{
    public const string SectionName = "OpenAI";

    public string ApiKey { get; init; } = string.Empty;

    [Required]
    [MinLength(1)]
    public string ChatModel { get; init; } = "gpt-4o-mini";

    [Required]
    [MinLength(1)]
    public string EmbeddingModel { get; init; } = "text-embedding-3-small";

    public bool EnableTools { get; init; } = true;

    public List<OpenAIModelPricingOptions> Pricing { get; init; } = [];
}

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

    [Required]
    public OpenAICircuitBreakerOptions CircuitBreaker { get; init; } = new();

    public List<OpenAIModelPricingOptions> Pricing { get; init; } = [];
}

public sealed class OpenAICircuitBreakerOptions
{
    public bool Enabled { get; init; } = true;

    [Range(0.01, 1.0)]
    public double FailureRatio { get; init; } = 0.5;

    [Range(2, 1000)]
    public int MinimumThroughput { get; init; } = 8;

    [Range(1, 3600)]
    public int SamplingDurationSeconds { get; init; } = 30;

    [Range(1, 3600)]
    public int BreakDurationSeconds { get; init; } = 30;
}

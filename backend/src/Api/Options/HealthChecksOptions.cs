using System.ComponentModel.DataAnnotations;

namespace Api.Options;

public sealed class HealthChecksOptions
{
    public const string SectionName = "HealthChecks";

    public bool EnableOpenAIConnectivityProbe { get; init; }

    [Required]
    [MinLength(1)]
    public string OpenAIProbeHost { get; init; } = "api.openai.com";

    [Range(100, 10000)]
    public int OpenAIProbeTimeoutMilliseconds { get; init; } = 1200;
}
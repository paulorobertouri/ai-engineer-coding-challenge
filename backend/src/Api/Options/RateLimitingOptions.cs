using System.ComponentModel.DataAnnotations;

namespace Api.Options;

public sealed class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    [Required]
    public RateLimitingPolicyOptions Chat { get; init; } = new();

    [Required]
    public RateLimitingPolicyOptions Ingest { get; init; } = new() { PermitLimit = 10 };
}

public sealed class RateLimitingPolicyOptions
{
    [Range(1, 1000)]
    public int PermitLimit { get; init; } = 30;

    [Range(1, 3600)]
    public int WindowSeconds { get; init; } = 60;

    [Range(0, 1000)]
    public int QueueLimit { get; init; } = 0;
}

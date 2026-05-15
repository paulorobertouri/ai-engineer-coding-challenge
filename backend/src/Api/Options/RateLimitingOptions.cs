using System.ComponentModel.DataAnnotations;

namespace Api.Options;

public sealed class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    [Required]
    [RegularExpression("^(inmemory|distributed)$")]
    public string Mode { get; init; } = RateLimitingMode.InMemory;

    [Required]
    public RateLimitingPolicyOptions Chat { get; init; } = new();

    [Required]
    public RateLimitingPolicyOptions Ingest { get; init; } = new() { PermitLimit = 10 };

    [Required]
    public DistributedRateLimitingOptions Distributed { get; init; } = new();
}

public static class RateLimitingMode
{
    public const string InMemory = "inmemory";
    public const string Distributed = "distributed";
}

public sealed class DistributedRateLimitingOptions
{
    [Required]
    [RegularExpression("^(memory|redis)$")]
    public string Provider { get; init; } = "memory";
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

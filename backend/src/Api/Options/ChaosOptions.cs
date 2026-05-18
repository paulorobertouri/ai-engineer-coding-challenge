using System.ComponentModel.DataAnnotations;

namespace Api.Options;

public sealed class ChaosOptions
{
    public const string SectionName = "Chaos";

    public bool Enabled { get; init; }

    [Range(0.0, 1.0)]
    public double FailureRate { get; init; } = 0.15;

    public int? Seed { get; init; }

    public IReadOnlyList<string> TargetPathPrefixes { get; init; } = ["/api/v1/chat", "/api/v1/ingest"];
}

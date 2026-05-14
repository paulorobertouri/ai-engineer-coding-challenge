using System.ComponentModel.DataAnnotations;

namespace Api.Options;

public sealed class TimeoutOptions
{
    public const string SectionName = "Timeouts";

    [Range(1, 600)]
    public int ChatSeconds { get; init; } = 30;

    [Range(1, 600)]
    public int IngestSeconds { get; init; } = 120;
}

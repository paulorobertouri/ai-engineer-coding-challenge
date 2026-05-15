using System.ComponentModel.DataAnnotations;

namespace Api.Options;

public sealed class ObservabilityOptions
{
    public const string SectionName = "Observability";

    public bool Enabled { get; init; } = true;

    public bool EnableConsoleExporter { get; init; } = true;

    [RegularExpression("^$|^https?://.+")]
    public string? OtlpEndpoint { get; init; }
}

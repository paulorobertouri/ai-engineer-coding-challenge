namespace Api.Contracts;

public sealed class ConfidenceIndicatorDto
{
    public const string High = "high";
    public const string Medium = "medium";
    public const string Low = "low";
    public const string NotFound = "not_found";

    public string Level { get; init; } = Low;

    public double EvidenceCoverage { get; init; }
}

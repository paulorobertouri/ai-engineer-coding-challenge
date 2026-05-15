namespace Api.Options;

public sealed class IngestJobsOptions
{
    public const string SectionName = "IngestJobs";

    public string Mode { get; init; } = "sync";

    public bool IsBackground => string.Equals(Mode, "background", StringComparison.OrdinalIgnoreCase);
}

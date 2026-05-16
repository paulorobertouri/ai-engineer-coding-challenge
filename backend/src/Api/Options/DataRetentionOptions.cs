using System.ComponentModel.DataAnnotations;

namespace Api.Options;

public sealed class DataRetentionOptions
{
    public const string SectionName = "DataRetention";

    [Range(1, 3650)]
    public int LogDays { get; init; } = 30;

    [Range(1, 3650)]
    public int AuditDays { get; init; } = 30;

    [Range(1, 3650)]
    public int FeedbackDays { get; init; } = 30;

    [Range(1, 3650)]
    public int UploadArtifactsDays { get; init; } = 7;

    [Range(1, 3650)]
    public int VectorStoreDays { get; init; } = 90;
}
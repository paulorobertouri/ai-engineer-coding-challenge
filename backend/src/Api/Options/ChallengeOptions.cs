using System.ComponentModel.DataAnnotations;

namespace Api.Options;

public sealed class ChallengeOptions
{
    public const string SectionName = "Challenge";

    [Required]
    [MinLength(1)]
    public string SourceDocumentPath { get; init; } = "../../../../knowledge-base/Grocery_Store_SOP.md";

    [Required]
    [MinLength(1)]
    public string VectorStorePath { get; init; } = "Data/vector-store.json";

    [Required]
    [MinLength(1)]
    public string IngestionAuditPath { get; init; } = "Data/ingestion-audit.json";
}

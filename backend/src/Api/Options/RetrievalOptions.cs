using System.ComponentModel.DataAnnotations;

namespace Api.Options;

public sealed class RetrievalOptions
{
    public const string SectionName = "Retrieval";

    [Range(1, 50)]
    public int TopK { get; init; } = 3;

    [Range(0.0, 1.0)]
    public double MinSimilarityScore { get; init; } = 0.3;

    public bool EnableQueryRewriting { get; init; } = true;

    public bool EnableReranking { get; init; } = true;

    [Range(1, 10)]
    public int RerankCandidateMultiplier { get; init; } = 3;
}

using System.ComponentModel.DataAnnotations;

namespace Api.Options;

public sealed class RetrievalBenchmarkOptions
{
    public const string SectionName = "RetrievalBenchmarks";

    [Required]
    [MinLength(1)]
    public string HistoryPath { get; init; } = "Data/retrieval-benchmark-history.json";

    [Range(1, 500)]
    public int MaxHistoryEntries { get; init; } = 200;
}

using System.ComponentModel.DataAnnotations;

namespace Api.Options;

public sealed class VectorStoreOptions
{
    public const string SectionName = "VectorStore";

    [Required]
    [MinLength(1)]
    public string Provider { get; init; } = "json";
}

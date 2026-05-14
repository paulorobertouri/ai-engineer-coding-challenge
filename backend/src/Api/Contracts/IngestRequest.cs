using System.ComponentModel.DataAnnotations;

namespace Api.Contracts;

public sealed class IngestRequest
{
    public const int MaxKnowledgeBaseIdLength = 64;

    public bool ForceReingest { get; init; }

    [MaxLength(MaxKnowledgeBaseIdLength, ErrorMessage = "KnowledgeBaseId must not exceed 64 characters.")]
    [RegularExpression("^[a-zA-Z0-9._-]*$", ErrorMessage = "KnowledgeBaseId may only contain letters, digits, '.', '_', or '-'.")]
    public string? KnowledgeBaseId { get; init; }
}
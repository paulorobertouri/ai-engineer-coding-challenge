using System.ComponentModel.DataAnnotations;

namespace Api.Contracts;

public sealed class SopMutationApprovalRequest
{
    [Required]
    [MinLength(1)]
    public string KnowledgeBaseId { get; init; } = string.Empty;

    [Required]
    [MinLength(1)]
    public string SourceChecksum { get; init; } = string.Empty;

    public string? Note { get; init; }
}

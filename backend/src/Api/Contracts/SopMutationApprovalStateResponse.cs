namespace Api.Contracts;

public sealed class SopMutationApprovalStateResponse
{
    public string KnowledgeBaseId { get; init; } = string.Empty;

    public string SourceChecksum { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public DateTimeOffset UpdatedAtUtc { get; init; }

    public string? Note { get; init; }
}

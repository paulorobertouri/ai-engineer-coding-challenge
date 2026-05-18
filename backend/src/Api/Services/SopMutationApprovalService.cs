using System.Collections.Concurrent;
using Api.Contracts;

namespace Api.Services;

public interface ISopMutationApprovalService
{
    SopMutationApprovalStateResponse RequestApproval(string knowledgeBaseId, string sourceChecksum, string? note);

    SopMutationApprovalStateResponse Approve(string knowledgeBaseId, string sourceChecksum, string? note);

    SopMutationApprovalStateResponse? GetState(string knowledgeBaseId, string sourceChecksum);

    bool IsApproved(string knowledgeBaseId, string sourceChecksum);
}

public sealed class InMemorySopMutationApprovalService : ISopMutationApprovalService
{
    private readonly ConcurrentDictionary<string, SopMutationApprovalStateResponse> _states = new(StringComparer.OrdinalIgnoreCase);

    public SopMutationApprovalStateResponse RequestApproval(string knowledgeBaseId, string sourceChecksum, string? note)
    {
        var key = BuildKey(knowledgeBaseId, sourceChecksum);
        var state = new SopMutationApprovalStateResponse
        {
            KnowledgeBaseId = knowledgeBaseId,
            SourceChecksum = sourceChecksum,
            Status = "pending",
            UpdatedAtUtc = DateTimeOffset.UtcNow,
            Note = note
        };

        _states[key] = state;
        return state;
    }

    public SopMutationApprovalStateResponse Approve(string knowledgeBaseId, string sourceChecksum, string? note)
    {
        var key = BuildKey(knowledgeBaseId, sourceChecksum);
        var state = new SopMutationApprovalStateResponse
        {
            KnowledgeBaseId = knowledgeBaseId,
            SourceChecksum = sourceChecksum,
            Status = "approved",
            UpdatedAtUtc = DateTimeOffset.UtcNow,
            Note = note
        };

        _states[key] = state;
        return state;
    }

    public SopMutationApprovalStateResponse? GetState(string knowledgeBaseId, string sourceChecksum)
    {
        return _states.TryGetValue(BuildKey(knowledgeBaseId, sourceChecksum), out var state)
            ? state
            : null;
    }

    public bool IsApproved(string knowledgeBaseId, string sourceChecksum)
    {
        var state = GetState(knowledgeBaseId, sourceChecksum);
        return state is not null && string.Equals(state.Status, "approved", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildKey(string knowledgeBaseId, string sourceChecksum)
    {
        return $"{knowledgeBaseId.Trim()}::{sourceChecksum.Trim()}";
    }
}

using System.ComponentModel.DataAnnotations;

namespace Api.Contracts;

public sealed class IngestJobPriorityUpdateRequest
{
    [Range(-10, 10)]
    public int Priority { get; init; }
}
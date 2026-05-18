using Api.Contracts;

namespace Api.Services;

public interface IIngestionAuditService
{
    Task RecordSuccessAsync(IngestionAuditRecord record, CancellationToken cancellationToken = default);

    Task RecordFailureAsync(IngestionAuditRecord record, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<IngestionAuditRecord>> ListAsync(CancellationToken cancellationToken = default);
}

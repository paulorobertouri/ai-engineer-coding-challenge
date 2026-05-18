using Api.Contracts;
using Api.Options;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace Api.Services;

public sealed class JsonIngestionAuditService(IOptions<ChallengeOptions> options, IWebHostEnvironment environment) : IIngestionAuditService
{
    private static readonly SemaphoreSlim AuditLock = new(1, 1);
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly string _auditPath = ResolveAuditPath(options.Value.IngestionAuditPath, environment.ContentRootPath);

    public async Task RecordSuccessAsync(IngestionAuditRecord record, CancellationToken cancellationToken = default)
    {
        await AppendAsync(record with { Outcome = "success" }, cancellationToken);
    }

    public async Task RecordFailureAsync(IngestionAuditRecord record, CancellationToken cancellationToken = default)
    {
        await AppendAsync(record with { Outcome = "failure" }, cancellationToken);
    }

    public async Task<IReadOnlyList<IngestionAuditRecord>> ListAsync(CancellationToken cancellationToken = default)
    {
        await AuditLock.WaitAsync(cancellationToken);
        try
        {
            return await LoadExistingAsync(cancellationToken);
        }
        finally
        {
            AuditLock.Release();
        }
    }

    private async Task AppendAsync(IngestionAuditRecord record, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(_auditPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await AuditLock.WaitAsync(cancellationToken);
        try
        {
            var records = await LoadExistingAsync(cancellationToken);
            records.Add(record with
            {
                TimestampUtc = record.TimestampUtc == default ? DateTimeOffset.UtcNow : record.TimestampUtc,
                SafeSummary = SensitiveDataRedactor.Sanitize(record.SafeSummary)
            });
            var json = JsonSerializer.Serialize(records, SerializerOptions);
            await File.WriteAllTextAsync(_auditPath, json, cancellationToken);
        }
        finally
        {
            AuditLock.Release();
        }
    }

    private async Task<List<IngestionAuditRecord>> LoadExistingAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_auditPath))
        {
            return [];
        }

        var json = await File.ReadAllTextAsync(_auditPath, cancellationToken);
        return JsonSerializer.Deserialize<List<IngestionAuditRecord>>(json, SerializerOptions) ?? [];
    }

    private static string ResolveAuditPath(string configuredPath, string contentRootPath)
    {
        if (Path.IsPathRooted(configuredPath))
        {
            return configuredPath;
        }

        return Path.GetFullPath(Path.Combine(contentRootPath, configuredPath));
    }
}

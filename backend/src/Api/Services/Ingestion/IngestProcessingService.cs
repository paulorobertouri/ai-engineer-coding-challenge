using Api.Contracts;
using Api.Models;
using Api.Observability;
using Api.Options;
using Microsoft.Extensions.Options;
using System.Diagnostics;

namespace Api.Services;

public sealed class IngestProcessingService(
    IOptions<ChallengeOptions> challengeOptions,
    IChunkingService chunkingService,
    IEmbeddingService embeddingService,
    IVectorStoreService vectorStoreService,
    IIngestionAuditService ingestionAuditService,
    ILogger<IngestProcessingService> logger,
    IWebHostEnvironment env) : IIngestProcessingService
{
    public async Task<IngestResponse> ProcessAsync(IngestJobRequest request, CancellationToken cancellationToken)
    {
        var processStopwatch = Stopwatch.StartNew();
        using var activity = AppTelemetry.ActivitySource.StartActivity("ingest.process");
        activity?.SetTag("ingest.knowledge_base_id", request.KnowledgeBaseId);
        activity?.SetTag("ingest.action", request.Action);
        activity?.SetTag("ingest.source_name", request.SourceName);

        var sourceChecksum = request.PrecomputedSourceChecksum ?? DocumentVersioning.ComputeSourceChecksum(request.SourceText);
        var documentVersion = DocumentVersioning.ComputeDefaultVersionLabel(sourceChecksum);
        var ingestedAtUtc = DateTimeOffset.UtcNow;
        var chunkingStopwatch = Stopwatch.StartNew();
        var chunks = await chunkingService.ChunkAsync(request.SourceText, request.SourceName, cancellationToken);
        chunkingStopwatch.Stop();
        AppTelemetry.IngestChunks.Add(chunks.Count);
        activity?.SetTag("ingest.chunk_count", chunks.Count);
        activity?.SetTag("ingest.chunking_ms", chunkingStopwatch.Elapsed.TotalMilliseconds);

        var records = new List<VectorRecord>();
        var existingById = request.ExistingRecords.ToDictionary(record => record.Id, StringComparer.Ordinal);
        var existingByHash = request.ExistingRecords
            .Select(record => new { Record = record, Hash = TryGetContentHash(record) })
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Hash))
            .GroupBy(entry => entry.Hash!, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().Record, StringComparer.Ordinal);
        var reusedEmbeddings = 0;
        var unchangedRecords = 0;
        var updatedRecords = 0;
        var newRecords = 0;
        var deletedRecords = request.ExistingRecords
            .Select(record => record.Id)
            .Except(chunks.Select(chunk => chunk.Id), StringComparer.Ordinal)
            .Count();

        foreach (var chunk in chunks)
        {
            if (existingById.TryGetValue(chunk.Id, out var existingRecord)
                && ChunkMatches(existingRecord, chunk))
            {
                records.Add(existingRecord);
                unchangedRecords++;
                continue;
            }

            float[] embedding;
            if (!string.IsNullOrWhiteSpace(chunk.ContentHash)
                && existingByHash.TryGetValue(chunk.ContentHash, out var cachedRecord)
                && cachedRecord.Embedding.Length > 0)
            {
                embedding = cachedRecord.Embedding;
                reusedEmbeddings++;
            }
            else
            {
                embedding = await embeddingService.EmbedAsync(chunk.Content, cancellationToken);
            }

            if (existingById.ContainsKey(chunk.Id))
            {
                updatedRecords++;
            }
            else
            {
                newRecords++;
            }

            var metadata = new Dictionary<string, string>
            {
                ["Index"] = chunk.Index.ToString()
            };

            if (chunk.StartLine.HasValue)
                metadata["StartLine"] = chunk.StartLine.Value.ToString();

            if (chunk.EndLine.HasValue)
                metadata["EndLine"] = chunk.EndLine.Value.ToString();

            if (!string.IsNullOrWhiteSpace(chunk.SectionTitle))
                metadata["SectionTitle"] = chunk.SectionTitle;

            if (!string.IsNullOrWhiteSpace(chunk.ContentHash))
                metadata["ContentHash"] = chunk.ContentHash;

            metadata[KnowledgeBaseScope.MetadataKey] = request.KnowledgeBaseId;
            metadata[DocumentVersioning.SourceChecksumMetadataKey] = sourceChecksum;
            metadata[DocumentVersioning.DocumentVersionMetadataKey] = documentVersion;
            metadata[DocumentVersioning.IngestedAtUtcMetadataKey] = ingestedAtUtc.ToString("O");

            records.Add(new VectorRecord
            {
                Id = chunk.Id,
                Source = chunk.Source,
                ChunkText = chunk.Content,
                Embedding = embedding,
                Metadata = metadata
            });
        }

        var recordsOutsideKnowledgeBase = request.AllExistingRecords
            .Where(record => !KnowledgeBaseScope.BelongsToKnowledgeBase(record, request.KnowledgeBaseId));
        var mergedRecords = recordsOutsideKnowledgeBase
            .Concat(records)
            .ToList();

        await vectorStoreService.SaveAsync(mergedRecords, cancellationToken);
        processStopwatch.Stop();
        AppTelemetry.IngestLatencyMs.Record(processStopwatch.Elapsed.TotalMilliseconds);
        activity?.SetTag("ingest.total_ms", processStopwatch.Elapsed.TotalMilliseconds);
        activity?.SetTag("ingest.records_saved", records.Count);

        logger.LogInformation(
            "[INGEST] Incremental ingest completed for knowledge base '{KnowledgeBaseId}'. Unchanged={Unchanged}, New={New}, Updated={Updated}, Deleted={Deleted}, ReusedEmbeddings={Reused}, Recomputed={Recomputed}, Total={Total}",
            request.KnowledgeBaseId,
            unchangedRecords,
            newRecords,
            updatedRecords,
            deletedRecords,
            reusedEmbeddings,
            newRecords + updatedRecords - reusedEmbeddings,
            records.Count);

        var vectorStorePath = challengeOptions.Value.VectorStorePath;

        await RecordSuccessAsync(
            request.Action,
            request.KnowledgeBaseId,
            request.SourceName,
            sourceChecksum,
            documentVersion,
            chunks.Count,
            records.Count,
            cancellationToken);

        return new IngestResponse
        {
            Accepted = true,
            Message = "Document ingested successfully.",
            SourcePath = request.DisplayPath,
            ChunksCreated = chunks.Count,
            RecordsPersisted = records.Count,
            VectorStorePath = vectorStorePath,
            KnowledgeBaseId = request.KnowledgeBaseId,
            DocumentVersion = documentVersion,
            SourceChecksum = sourceChecksum,
            IngestedAtUtc = ingestedAtUtc,
            IsPlaceholder = false,
            JobId = request.JobId,
            JobStatus = "succeeded",
            JobStatusUrl = $"/api/v1/ingest/jobs/{request.JobId}"
        };
    }

    private Task RecordSuccessAsync(
        string action,
        string knowledgeBaseId,
        string sourceName,
        string sourceChecksum,
        string documentVersion,
        int chunkCount,
        int recordsPersisted,
        CancellationToken cancellationToken)
    {
        return ingestionAuditService.RecordSuccessAsync(new IngestionAuditRecord
        {
            TimestampUtc = DateTimeOffset.UtcNow,
            Outcome = "success",
            Action = action,
            KnowledgeBaseId = knowledgeBaseId,
            SourceName = sourceName,
            SourceChecksum = sourceChecksum,
            DocumentVersion = documentVersion,
            ChunkCount = chunkCount,
            RecordsPersisted = recordsPersisted,
            TriggeredBy = env.EnvironmentName
        }, cancellationToken);
    }

    private static string? TryGetContentHash(VectorRecord record)
    {
        if (!record.Metadata.TryGetValue("ContentHash", out var contentHash))
        {
            return null;
        }

        return string.IsNullOrWhiteSpace(contentHash) ? null : contentHash;
    }

    private static bool ChunkMatches(VectorRecord existingRecord, TextChunk chunk)
    {
        if (!string.Equals(existingRecord.Source, chunk.Source, StringComparison.Ordinal) ||
            !string.Equals(existingRecord.ChunkText, chunk.Content, StringComparison.Ordinal))
        {
            return false;
        }

        var existingHash = TryGetContentHash(existingRecord);
        if (string.IsNullOrWhiteSpace(existingHash) || string.IsNullOrWhiteSpace(chunk.ContentHash))
        {
            return true;
        }

        return string.Equals(existingHash, chunk.ContentHash, StringComparison.Ordinal);
    }
}

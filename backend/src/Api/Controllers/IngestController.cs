using Api.Contracts;
using Api.Models;
using Api.Options;
using Api.Services;
using Api.Security;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[EnableRateLimiting("ingest")]
public sealed class IngestController(
    IOptions<ChallengeOptions> challengeOptions,
    IOptions<UploadOptions> uploadOptions,
    IOptions<TimeoutOptions> timeoutOptions,
    IChunkingService chunkingService,
    IDocumentExtractionService documentExtractionService,
    IEmbeddingService embeddingService,
    IVectorStoreService vectorStoreService,
    IIngestionAuditService ingestionAuditService,
    ILogger<IngestController> logger,
    IWebHostEnvironment env) : ControllerBase
{
    private static readonly SemaphoreSlim IngestLock = new(1, 1);
    private const int PreviewSampleLength = 180;

    private const string ResetConfirmationValue = "RESET";

    // ── POST /api/v1/ingest  (default SOP from server-side path) ─────────────

    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.KnowledgeAdmin)]
    public async Task<ActionResult<IngestResponse>> Post([FromBody] IngestRequest? request, CancellationToken cancellationToken)
    {
        var knowledgeBaseId = KnowledgeBaseScope.Normalize(request?.KnowledgeBaseId);
        var configuredSourcePath = challengeOptions.Value.SourceDocumentPath;

        return await ExecuteWithIngestTimeoutAsync(async operationToken =>
            await ExecuteExclusiveIngestAsync(async () =>
        {
            var existingRecords = await vectorStoreService.LoadAsync(operationToken);
            var existingRecordsForKnowledgeBase = existingRecords
                .Where(record => KnowledgeBaseScope.BelongsToKnowledgeBase(record, knowledgeBaseId))
                .ToList();
            var forceReingest = request?.ForceReingest ?? false;

            logger.LogInformation("[INGEST] Configured: {ConfiguredPath} | ContentRoot: {ContentRoot}",
                challengeOptions.Value.SourceDocumentPath, env.ContentRootPath);

            var sourcePath = Path.IsPathRooted(configuredSourcePath)
                ? configuredSourcePath
                : Path.GetFullPath(Path.Combine(env.ContentRootPath, configuredSourcePath));

            logger.LogInformation("[INGEST] Resolved: {ResolvedPath}", sourcePath);

            if (!System.IO.File.Exists(sourcePath))
            {
                var localFallback = Path.GetFullPath(Path.Combine(env.ContentRootPath, "../../../../knowledge-base/Grocery_Store_SOP.md"));
                logger.LogInformation("[INGEST] Fallback: {FallbackPath}", localFallback);
                if (System.IO.File.Exists(localFallback))
                    sourcePath = localFallback;
                else
                {
                    await RecordFailureAsync(
                        action: "default-ingest",
                        knowledgeBaseId,
                        configuredSourcePath,
                        "source document not found",
                        cancellationToken);
                    return NotFound(ApiErrorFactory.NotFound("Source document not found.", "Source document not found."));
                }
            }

            var sourceName = System.IO.Path.GetFileName(sourcePath);

            string sourceText;
            try
            {
                sourceText = await documentExtractionService.ExtractTextFromFileAsync(sourcePath, operationToken);
            }
            catch (DocumentExtractionException ex)
            {
                await RecordFailureAsync(
                    action: "default-ingest",
                    knowledgeBaseId,
                    sourceName,
                    ex.Message,
                    cancellationToken);
                return BadRequest(ApiErrorFactory.BadRequest("Unsupported source document.", ex.Message));
            }

            var sourceChecksum = DocumentVersioning.ComputeSourceChecksum(sourceText);
            if (TryFindDuplicate(existingRecordsForKnowledgeBase, sourceChecksum, out var duplicateVersion))
            {
                var duplicateMessage = $"This document has already been ingested as version '{duplicateVersion}' (checksum: {sourceChecksum}).";
                await RecordFailureAsync(
                    action: "default-ingest",
                    knowledgeBaseId,
                    sourceName,
                    duplicateMessage,
                    cancellationToken);
                return Conflict(ApiErrorFactory.Conflict("Document already ingested.", duplicateMessage));
            }

            if (existingRecordsForKnowledgeBase.Count > 0 && !forceReingest)
            {
                logger.LogWarning(
                    "[INGEST] Rejected: knowledge base '{KnowledgeBaseId}' already contains {Count} records.",
                    knowledgeBaseId,
                    existingRecordsForKnowledgeBase.Count);
                await RecordFailureAsync(
                    action: "default-ingest",
                    knowledgeBaseId,
                    configuredSourcePath,
                    "knowledge base already ingested",
                    cancellationToken);
                return Conflict(ApiErrorFactory.Conflict(
                    "Knowledge base already ingested.",
                    "The knowledge base has already been ingested. Re-ingestion is not permitted."));
            }

            if (existingRecordsForKnowledgeBase.Count > 0 && forceReingest)
            {
                logger.LogInformation(
                    "[INGEST] Force reingest requested for knowledge base '{KnowledgeBaseId}'. Existing records: {Count}",
                    knowledgeBaseId,
                    existingRecordsForKnowledgeBase.Count);
            }

            return await ProcessIngestAsync(
                sourceText,
                sourceName,
                sourcePath,
                knowledgeBaseId,
                existingRecords,
                existingRecordsForKnowledgeBase,
                "default-ingest",
                sourceChecksum,
                operationToken);
        }, operationToken), cancellationToken);
    }

    // ── POST /api/v1/ingest/upload  (user-supplied file) ─────────────────────

    [HttpPost("upload")]
    [Authorize(Policy = AuthorizationPolicies.KnowledgeAdmin)]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<IngestResponse>> Upload(IFormFile? file, CancellationToken cancellationToken, [FromQuery] string? knowledgeBaseId = null)
    {
        var scopedKnowledgeBaseId = KnowledgeBaseScope.Normalize(knowledgeBaseId);

        return await ExecuteWithIngestTimeoutAsync(async operationToken =>
            await ExecuteExclusiveIngestAsync(async () =>
        {
            var existingRecords = await vectorStoreService.LoadAsync(operationToken);
            var existingRecordsForKnowledgeBase = existingRecords
                .Where(record => KnowledgeBaseScope.BelongsToKnowledgeBase(record, scopedKnowledgeBaseId))
                .ToList();

            if (file is null || file.Length == 0)
            {
                await RecordFailureAsync(
                    action: "upload-ingest",
                    scopedKnowledgeBaseId,
                    file?.FileName ?? "unknown",
                    "no file provided",
                    cancellationToken);
                return BadRequest(ApiErrorFactory.BadRequest("Invalid upload.", "No file provided."));
            }

            if (file.Length > uploadOptions.Value.MaxUploadBytes)
            {
                await RecordFailureAsync(
                    action: "upload-ingest",
                    scopedKnowledgeBaseId,
                    file.FileName,
                    "file exceeds upload limit",
                    cancellationToken);
                return BadRequest(ApiErrorFactory.BadRequest(
                    "Invalid upload.",
                    $"File exceeds the {uploadOptions.Value.MaxUploadBytes / (1024 * 1024)} MB limit."));
            }

            var ext = Path.GetExtension(file.FileName);
            if (!documentExtractionService.IsSupportedExtension(ext))
            {
                await RecordFailureAsync(
                    action: "upload-ingest",
                    scopedKnowledgeBaseId,
                    file.FileName,
                    "unsupported file type",
                    cancellationToken);
                return BadRequest(ApiErrorFactory.BadRequest(
                    "Invalid upload.",
                    $"Unsupported file type '{ext}'. Supported formats: {documentExtractionService.DescribeSupportedFormats()}"));
            }

            // Use only the filename (never the path) to prevent path-traversal (OWASP A01).
            var sourceName = Path.GetFileName(file.FileName);

            string sourceText;
            try
            {
                await using var uploadStream = file.OpenReadStream();
                sourceText = await documentExtractionService.ExtractTextAsync(sourceName, uploadStream, operationToken);
            }
            catch (DocumentExtractionException ex)
            {
                await RecordFailureAsync(
                    action: "upload-ingest",
                    scopedKnowledgeBaseId,
                    sourceName,
                    ex.Message,
                    cancellationToken);
                return BadRequest(ApiErrorFactory.BadRequest("Invalid upload.", ex.Message));
            }

            var sourceChecksum = DocumentVersioning.ComputeSourceChecksum(sourceText);
            if (TryFindDuplicate(existingRecordsForKnowledgeBase, sourceChecksum, out var duplicateVersion))
            {
                var duplicateMessage = $"This document has already been ingested as version '{duplicateVersion}' (checksum: {sourceChecksum}).";
                await RecordFailureAsync(
                    action: "upload-ingest",
                    scopedKnowledgeBaseId,
                    sourceName,
                    duplicateMessage,
                    cancellationToken);
                return Conflict(ApiErrorFactory.Conflict("Document already ingested.", duplicateMessage));
            }

            if (existingRecordsForKnowledgeBase.Count > 0)
            {
                logger.LogWarning(
                    "[INGEST] Upload rejected: knowledge base '{KnowledgeBaseId}' already has {Count} records.",
                    scopedKnowledgeBaseId,
                    existingRecordsForKnowledgeBase.Count);
                await RecordFailureAsync(
                    action: "upload-ingest",
                    scopedKnowledgeBaseId,
                    sourceName,
                    "knowledge base already ingested",
                    cancellationToken);
                return Conflict(ApiErrorFactory.Conflict(
                    "Knowledge base already ingested.",
                    "The knowledge base has already been ingested. Re-ingestion is not permitted."));
            }

            logger.LogInformation("[INGEST] Upload received: {FileName} ({Bytes} bytes)", sourceName, file.Length);

            return await ProcessIngestAsync(
                sourceText,
                sourceName,
                sourceName,
                scopedKnowledgeBaseId,
                existingRecords,
                existingRecordsForKnowledgeBase,
                "upload-ingest",
                sourceChecksum,
                operationToken);
        }, operationToken), cancellationToken);
    }

    [HttpPost("preview")]
    [Authorize(Policy = AuthorizationPolicies.Operator)]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<IngestPreviewResponse>> Preview(IFormFile? file, CancellationToken cancellationToken)
    {
        return await ExecuteWithIngestTimeoutAsync<IngestPreviewResponse>(async operationToken =>
        {
            string sourceName;
            string sourceText;

            if (file is not null)
            {
                if (file.Length == 0)
                {
                    return BadRequest(ApiErrorFactory.BadRequest("Invalid upload.", "No file provided."));
                }

                if (file.Length > uploadOptions.Value.MaxUploadBytes)
                {
                    return BadRequest(ApiErrorFactory.BadRequest(
                        "Invalid upload.",
                        $"File exceeds the {uploadOptions.Value.MaxUploadBytes / (1024 * 1024)} MB limit."));
                }

                var extension = Path.GetExtension(file.FileName);
                if (!documentExtractionService.IsSupportedExtension(extension))
                {
                    return BadRequest(ApiErrorFactory.BadRequest(
                        "Invalid upload.",
                        $"Unsupported file type '{extension}'. Supported formats: {documentExtractionService.DescribeSupportedFormats()}"));
                }

                sourceName = Path.GetFileName(file.FileName);
                try
                {
                    await using var uploadStream = file.OpenReadStream();
                    sourceText = await documentExtractionService.ExtractTextAsync(sourceName, uploadStream, operationToken);
                }
                catch (DocumentExtractionException ex)
                {
                    return BadRequest(ApiErrorFactory.BadRequest("Invalid upload.", ex.Message));
                }
            }
            else
            {
                var configuredSourcePath = challengeOptions.Value.SourceDocumentPath;
                var sourcePath = Path.IsPathRooted(configuredSourcePath)
                    ? configuredSourcePath
                    : Path.GetFullPath(Path.Combine(env.ContentRootPath, configuredSourcePath));

                if (!System.IO.File.Exists(sourcePath))
                {
                    var localFallback = Path.GetFullPath(Path.Combine(env.ContentRootPath, "../../../../knowledge-base/Grocery_Store_SOP.md"));
                    if (System.IO.File.Exists(localFallback))
                    {
                        sourcePath = localFallback;
                    }
                    else
                    {
                        return NotFound(ApiErrorFactory.NotFound("Source document not found.", "Source document not found."));
                    }
                }

                sourceName = Path.GetFileName(sourcePath);
                try
                {
                    sourceText = await documentExtractionService.ExtractTextFromFileAsync(sourcePath, operationToken);
                }
                catch (DocumentExtractionException ex)
                {
                    return BadRequest(ApiErrorFactory.BadRequest("Unsupported source document.", ex.Message));
                }
            }

            var chunks = await chunkingService.ChunkAsync(sourceText, sourceName, operationToken);

            return Ok(new IngestPreviewResponse
            {
                Accepted = true,
                Message = "Preview generated successfully.",
                SourceName = sourceName,
                ChunkCount = chunks.Count,
                Chunks = chunks.Select(chunk => new IngestPreviewChunk
                {
                    Id = chunk.Id,
                    SectionTitle = chunk.SectionTitle ?? string.Empty,
                    CharacterCount = chunk.Content.Length,
                    SampleText = chunk.Content.Length <= PreviewSampleLength
                        ? chunk.Content
                        : chunk.Content[..PreviewSampleLength]
                }).ToList()
            });
        }, cancellationToken);
    }

    [HttpDelete("reset")]
    [Authorize(Policy = AuthorizationPolicies.KnowledgeAdmin)]
    public async Task<ActionResult<object>> Reset([FromQuery] string? confirm, CancellationToken cancellationToken)
    {
        if (!env.IsDevelopment())
            return NotFound(ApiErrorFactory.NotFound(
                "Reset endpoint unavailable.",
                "Reset endpoint is only available in Development."));

        if (!string.Equals(confirm, ResetConfirmationValue, StringComparison.Ordinal))
            return BadRequest(ApiErrorFactory.BadRequest(
                "Reset confirmation required.",
                $"Reset requires explicit confirmation. Call with '?confirm={ResetConfirmationValue}'."));

        return await ExecuteExclusiveIngestAsync<object>(async () =>
        {
            var existingRecords = await vectorStoreService.LoadAsync(cancellationToken);
            await vectorStoreService.SaveAsync([], cancellationToken);

            logger.LogWarning("[INGEST] Knowledge base reset executed. Removed {Count} records.", existingRecords.Count);

            return Ok((object)new
            {
                accepted = true,
                message = "Knowledge base reset completed. Reingestion is now allowed.",
                deletedRecords = existingRecords.Count,
                vectorStorePath = challengeOptions.Value.VectorStorePath
            });
        }, cancellationToken);
    }

    // ── Shared chunk → embed → persist pipeline ───────────────────────────────

    private static async Task<ActionResult<T>> ExecuteExclusiveIngestAsync<T>(
        Func<Task<ActionResult<T>>> action,
        CancellationToken cancellationToken)
    {
        await IngestLock.WaitAsync(cancellationToken);
        try
        {
            return await action();
        }
        finally
        {
            IngestLock.Release();
        }
    }

    private async Task<ActionResult<T>> ExecuteWithIngestTimeoutAsync<T>(
        Func<CancellationToken, Task<ActionResult<T>>> operation,
        CancellationToken cancellationToken)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(timeoutOptions.Value.IngestSeconds));

        try
        {
            return await operation(timeoutCts.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            var details = ApiErrorFactory.RequestTimeout(
                "Ingest request timed out.",
                "The ingest request took too long to complete. Please retry.",
                HttpContext?.Request.Path.Value);
            return StatusCode(StatusCodes.Status408RequestTimeout, details);
        }
    }

    private async Task<ActionResult<IngestResponse>> ProcessIngestAsync(
        string sourceText,
        string sourceName,
        string displayPath,
        string knowledgeBaseId,
        IReadOnlyList<VectorRecord> allExistingRecords,
        IReadOnlyList<VectorRecord> existingRecords,
        string action,
        string? precomputedSourceChecksum,
        CancellationToken cancellationToken)
    {
        var sourceChecksum = precomputedSourceChecksum ?? DocumentVersioning.ComputeSourceChecksum(sourceText);
        var documentVersion = DocumentVersioning.ComputeDefaultVersionLabel(sourceChecksum);
        var ingestedAtUtc = DateTimeOffset.UtcNow;
        var chunks = await chunkingService.ChunkAsync(sourceText, sourceName, cancellationToken);
        var records = new List<VectorRecord>();
        var existingById = existingRecords.ToDictionary(record => record.Id, StringComparer.Ordinal);
        var existingByHash = existingRecords
            .Select(record => new { Record = record, Hash = TryGetContentHash(record) })
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Hash))
            .GroupBy(entry => entry.Hash!, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().Record, StringComparer.Ordinal);
        var reusedEmbeddings = 0;
        var unchangedRecords = 0;
        var updatedRecords = 0;
        var newRecords = 0;
        var deletedRecords = existingRecords
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

            metadata[KnowledgeBaseScope.MetadataKey] = knowledgeBaseId;
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

        var recordsOutsideKnowledgeBase = allExistingRecords
            .Where(record => !KnowledgeBaseScope.BelongsToKnowledgeBase(record, knowledgeBaseId));
        var mergedRecords = recordsOutsideKnowledgeBase
            .Concat(records)
            .ToList();

        await vectorStoreService.SaveAsync(mergedRecords, cancellationToken);
        logger.LogInformation(
            "[INGEST] Incremental ingest completed for knowledge base '{KnowledgeBaseId}'. Unchanged={Unchanged}, New={New}, Updated={Updated}, Deleted={Deleted}, ReusedEmbeddings={Reused}, Recomputed={Recomputed}, Total={Total}",
            knowledgeBaseId,
            unchangedRecords,
            newRecords,
            updatedRecords,
            deletedRecords,
            reusedEmbeddings,
            newRecords + updatedRecords - reusedEmbeddings,
            records.Count);

        var vectorStorePath = challengeOptions.Value.VectorStorePath;

        await RecordSuccessAsync(
            action,
            knowledgeBaseId,
            sourceName,
            sourceChecksum,
            documentVersion,
            chunks.Count,
            records.Count,
            cancellationToken);

        return Ok(new IngestResponse
        {
            Accepted = true,
            Message = "Document ingested successfully.",
            SourcePath = displayPath,
            ChunksCreated = chunks.Count,
            RecordsPersisted = records.Count,
            VectorStorePath = vectorStorePath,
            KnowledgeBaseId = knowledgeBaseId,
            DocumentVersion = documentVersion,
            SourceChecksum = sourceChecksum,
            IngestedAtUtc = ingestedAtUtc,
            IsPlaceholder = false
        });
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

    private Task RecordFailureAsync(
        string action,
        string knowledgeBaseId,
        string sourceName,
        string safeSummary,
        CancellationToken cancellationToken)
    {
        return ingestionAuditService.RecordFailureAsync(new IngestionAuditRecord
        {
            TimestampUtc = DateTimeOffset.UtcNow,
            Outcome = "failure",
            Action = action,
            KnowledgeBaseId = knowledgeBaseId,
            SourceName = sourceName,
            SafeSummary = safeSummary,
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

    private static bool TryFindDuplicate(
        IEnumerable<VectorRecord> existingRecords,
        string sourceChecksum,
        out string duplicateVersion)
    {
        var duplicate = existingRecords.FirstOrDefault(record =>
            record.Metadata.TryGetValue(DocumentVersioning.SourceChecksumMetadataKey, out var existingChecksum)
            && string.Equals(existingChecksum, sourceChecksum, StringComparison.Ordinal));

        if (duplicate is null)
        {
            duplicateVersion = "unknown";
            return false;
        }

        if (!duplicate.Metadata.TryGetValue(DocumentVersioning.DocumentVersionMetadataKey, out var version)
            || string.IsNullOrWhiteSpace(version))
        {
            duplicateVersion = "unknown";
        }
        else
        {
            duplicateVersion = version;
        }

        return true;
    }
}
using Api.Application.Ingest;
using Api.Contracts;
using Api.Models;
using Api.Observability;
using Api.Options;
using Api.Services;
using Api.Security;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Diagnostics;

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
    IVectorStoreService vectorStoreService,
    IIngestionAuditService ingestionAuditService,
    IIngestJobDispatcher ingestJobDispatcher,
    IIngestJobStatusStore ingestJobStatusStore,
    ILogger<IngestController> logger,
    IWebHostEnvironment env) : ControllerBase
{
    private static readonly SemaphoreSlim IngestLock = new(1, 1);
    private const int PreviewSampleLength = 180;
    private const int MaxUploadedLineLength = 10_000;
    private readonly ResetKnowledgeBaseHandler _resetKnowledgeBaseHandler =
        new(env, vectorStoreService, challengeOptions, logger);

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

            return await SubmitIngestJobAsync(
                sourceText,
                sourceName,
                sourcePath,
                knowledgeBaseId,
                existingRecords,
                existingRecordsForKnowledgeBase,
                "default-ingest",
                sourceChecksum,
                forceReingest,
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

                if (!TryValidateUploadedText(sourceText, out var validationMessage))
                {
                    await RecordFailureAsync(
                        action: "upload-ingest",
                        scopedKnowledgeBaseId,
                        sourceName,
                        validationMessage,
                        cancellationToken);
                    return BadRequest(ApiErrorFactory.BadRequest("Invalid upload.", validationMessage));
                }
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

            return await SubmitIngestJobAsync(
                sourceText,
                sourceName,
                sourceName,
                scopedKnowledgeBaseId,
                existingRecords,
                existingRecordsForKnowledgeBase,
                "upload-ingest",
                sourceChecksum,
                false,
                operationToken);
        }, operationToken), cancellationToken);
    }

    private static bool TryValidateUploadedText(string sourceText, out string message)
    {
        if (string.IsNullOrWhiteSpace(sourceText))
        {
            message = "The uploaded file did not contain readable text content.";
            return false;
        }

        var lines = sourceText.Split('\n');
        if (lines.Any(line => line.TrimEnd('\r').Length > MaxUploadedLineLength))
        {
            message = $"The uploaded file contains lines longer than {MaxUploadedLineLength} characters.";
            return false;
        }

        message = string.Empty;
        return true;
    }

    [HttpGet("jobs/{jobId:guid}")]
    [Authorize(Policy = AuthorizationPolicies.KnowledgeAdmin)]
    public ActionResult<IngestJobStatusResponse> GetJobStatus(Guid jobId)
    {
        var job = ingestJobStatusStore.Get(jobId);
        if (job is null)
        {
            return NotFound(ApiErrorFactory.NotFound(
                "Ingest job not found.",
                $"No ingest job exists for '{jobId}'."));
        }

        return Ok(new IngestJobStatusResponse
        {
            JobId = job.JobId,
            KnowledgeBaseId = job.KnowledgeBaseId,
            Status = job.State.ToString().ToLowerInvariant(),
            Message = job.State switch
            {
                IngestJobState.Queued => "Ingest job queued.",
                IngestJobState.Running => "Ingest job is running.",
                IngestJobState.Succeeded => job.Response?.Message ?? "Ingest job completed successfully.",
                IngestJobState.Failed => job.ErrorMessage ?? "Ingest job failed.",
                _ => "Unknown job state."
            },
            QueuedAtUtc = job.QueuedAtUtc,
            StartedAtUtc = job.StartedAtUtc,
            CompletedAtUtc = job.CompletedAtUtc,
            Result = job.Response,
            ErrorMessage = job.ErrorMessage
        });
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

                    if (!TryValidateUploadedText(sourceText, out var validationMessage))
                    {
                        return BadRequest(ApiErrorFactory.BadRequest("Invalid upload.", validationMessage));
                    }
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
        return await ExecuteExclusiveIngestAsync(
            () => _resetKnowledgeBaseHandler.HandleAsync(
                new ResetKnowledgeBaseCommand(confirm),
                cancellationToken),
            cancellationToken);
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

    private async Task<ActionResult<IngestResponse>> SubmitIngestJobAsync(
        string sourceText,
        string sourceName,
        string displayPath,
        string knowledgeBaseId,
        IReadOnlyList<VectorRecord> allExistingRecords,
        IReadOnlyList<VectorRecord> existingRecords,
        string action,
        string? precomputedSourceChecksum,
        bool forceReingest,
        CancellationToken cancellationToken)
    {
        if (ingestJobStatusStore.HasActiveJob(knowledgeBaseId))
        {
            return Conflict(ApiErrorFactory.Conflict(
                "Knowledge base already has an active ingest job.",
                "Another ingest job is already queued or running for this knowledge base."));
        }

        var jobRequest = new IngestJobRequest(
            Guid.NewGuid(),
            action,
            knowledgeBaseId,
            sourceText,
            sourceName,
            displayPath,
            allExistingRecords,
            existingRecords,
            precomputedSourceChecksum,
            forceReingest);

        try
        {
            var submission = await ingestJobDispatcher.SubmitAsync(jobRequest, cancellationToken);
            return submission.IsBackground ? Accepted(submission.Response) : Ok(submission.Response);
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(ex, "Ingest job submission rejected. KnowledgeBaseId={KnowledgeBaseId}", knowledgeBaseId);
            return Conflict(ApiErrorFactory.Conflict(
                "Knowledge base already has an active ingest job.",
                ex.Message));
        }
    }
}
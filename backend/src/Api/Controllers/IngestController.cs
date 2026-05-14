using Api.Contracts;
using Api.Models;
using Api.Options;
using Api.Services;
using Asp.Versioning;
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
    IChunkingService chunkingService,
    IEmbeddingService embeddingService,
    IVectorStoreService vectorStoreService,
    ILogger<IngestController> logger,
    IWebHostEnvironment env) : ControllerBase
{
    private static readonly SemaphoreSlim IngestLock = new(1, 1);

    private static readonly HashSet<string> AllowedExtensions =
        new([".md", ".txt"], StringComparer.OrdinalIgnoreCase);
    private const string ResetConfirmationValue = "RESET";

    // ── POST /api/v1/ingest  (default SOP from server-side path) ─────────────

    [HttpPost]
    public async Task<ActionResult<IngestResponse>> Post([FromBody] IngestRequest? request, CancellationToken cancellationToken)
    {
        return await ExecuteExclusiveIngestAsync(async () =>
        {
            var existingRecords = await vectorStoreService.LoadAsync(cancellationToken);
            var forceReingest = request?.ForceReingest ?? false;
            if (existingRecords.Count > 0 && !forceReingest)
            {
                logger.LogWarning("[INGEST] Rejected: vector store already contains {Count} records.", existingRecords.Count);
                return Conflict(ApiErrorFactory.Conflict(
                    "Knowledge base already ingested.",
                    "The knowledge base has already been ingested. Re-ingestion is not permitted."));
            }

            if (existingRecords.Count > 0 && forceReingest)
            {
                logger.LogInformation("[INGEST] Force reingest requested. Existing records: {Count}", existingRecords.Count);
            }

            var configuredSourcePath = challengeOptions.Value.SourceDocumentPath;

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
                    return NotFound(ApiErrorFactory.NotFound("Source document not found.", "Source document not found."));
            }

            var sourceText = await System.IO.File.ReadAllTextAsync(sourcePath, cancellationToken);
            var sourceName = System.IO.Path.GetFileName(sourcePath);

            return await ProcessIngestAsync(sourceText, sourceName, sourcePath, existingRecords, cancellationToken);
        }, cancellationToken);
    }

    // ── POST /api/v1/ingest/upload  (user-supplied file) ─────────────────────

    [HttpPost("upload")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<IngestResponse>> Upload(IFormFile? file, CancellationToken cancellationToken)
    {
        return await ExecuteExclusiveIngestAsync(async () =>
        {
            var existingRecords = await vectorStoreService.LoadAsync(cancellationToken);
            if (existingRecords.Count > 0)
            {
                logger.LogWarning("[INGEST] Upload rejected: store already has {Count} records.", existingRecords.Count);
                return Conflict(ApiErrorFactory.Conflict(
                    "Knowledge base already ingested.",
                    "The knowledge base has already been ingested. Re-ingestion is not permitted."));
            }

            if (file is null || file.Length == 0)
                return BadRequest(ApiErrorFactory.BadRequest("Invalid upload.", "No file provided."));

            if (file.Length > uploadOptions.Value.MaxUploadBytes)
                return BadRequest(ApiErrorFactory.BadRequest(
                    "Invalid upload.",
                    $"File exceeds the {uploadOptions.Value.MaxUploadBytes / (1024 * 1024)} MB limit."));

            var ext = Path.GetExtension(file.FileName);
            if (!AllowedExtensions.Contains(ext))
                return BadRequest(ApiErrorFactory.BadRequest("Invalid upload.", "Only .md and .txt files are accepted."));

            // Use only the filename (never the path) to prevent path-traversal (OWASP A01).
            var sourceName = Path.GetFileName(file.FileName);

            string sourceText;
            using (var reader = new System.IO.StreamReader(file.OpenReadStream(), System.Text.Encoding.UTF8))
                sourceText = await reader.ReadToEndAsync(cancellationToken);

            logger.LogInformation("[INGEST] Upload received: {FileName} ({Bytes} bytes)", sourceName, file.Length);

            return await ProcessIngestAsync(sourceText, sourceName, sourceName, [], cancellationToken);
        }, cancellationToken);
    }

    [HttpDelete("reset")]
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

        return await ExecuteExclusiveIngestAsync(async () =>
        {
            var existingRecords = await vectorStoreService.LoadAsync(cancellationToken);
            await vectorStoreService.SaveAsync([], cancellationToken);

            logger.LogWarning("[INGEST] Knowledge base reset executed. Removed {Count} records.", existingRecords.Count);

            return Ok(new
            {
                accepted = true,
                message = "Knowledge base reset completed. Reingestion is now allowed.",
                deletedRecords = existingRecords.Count,
                vectorStorePath = challengeOptions.Value.VectorStorePath
            });
        }, cancellationToken);
    }

    // ── Shared chunk → embed → persist pipeline ───────────────────────────────

    private static async Task<ActionResult<IngestResponse>> ExecuteExclusiveIngestAsync(
        Func<Task<ActionResult<IngestResponse>>> action,
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

    private async Task<ActionResult<IngestResponse>> ProcessIngestAsync(
        string sourceText,
        string sourceName,
        string displayPath,
        IReadOnlyList<VectorRecord> existingRecords,
        CancellationToken cancellationToken)
    {
        var chunks = await chunkingService.ChunkAsync(sourceText, sourceName, cancellationToken);
        var records = new List<VectorRecord>();
        var existingByHash = existingRecords
            .Select(record => new { Record = record, Hash = TryGetContentHash(record) })
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Hash))
            .GroupBy(entry => entry.Hash!, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().Record, StringComparer.Ordinal);
        var reusedEmbeddings = 0;

        foreach (var chunk in chunks)
        {
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

            records.Add(new VectorRecord
            {
                Id = chunk.Id,
                Source = chunk.Source,
                ChunkText = chunk.Content,
                Embedding = embedding,
                Metadata = metadata
            });
        }

        await vectorStoreService.SaveAsync(records, cancellationToken);
        logger.LogInformation(
            "[INGEST] Embedding reuse completed. Reused={Reused}, Recomputed={Recomputed}, Total={Total}",
            reusedEmbeddings,
            records.Count - reusedEmbeddings,
            records.Count);

        var vectorStorePath = challengeOptions.Value.VectorStorePath;

        return Ok(new IngestResponse
        {
            Accepted = true,
            Message = "Document ingested successfully.",
            SourcePath = displayPath,
            ChunksCreated = chunks.Count,
            RecordsPersisted = records.Count,
            VectorStorePath = vectorStorePath,
            IsPlaceholder = false
        });
    }

    private static string? TryGetContentHash(VectorRecord record)
    {
        if (!record.Metadata.TryGetValue("ContentHash", out var contentHash))
        {
            return null;
        }

        return string.IsNullOrWhiteSpace(contentHash) ? null : contentHash;
    }
}
using Api.Contracts;
using Api.Models;
using Api.Services;
using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[EnableRateLimiting("ingest")]
public sealed class IngestController(
    IConfiguration configuration,
    IChunkingService chunkingService,
    IEmbeddingService embeddingService,
    IVectorStoreService vectorStoreService,
    ILogger<IngestController> logger,
    IWebHostEnvironment env) : ControllerBase
{
    private static readonly HashSet<string> AllowedExtensions =
        new([".md", ".txt"], StringComparer.OrdinalIgnoreCase);

    private const long MaxUploadBytes = 10 * 1024 * 1024; // 10 MB

    // ── POST /api/v1/ingest  (default SOP from server-side path) ─────────────

    [HttpPost]
    public async Task<ActionResult<IngestResponse>> Post([FromBody] IngestRequest? request, CancellationToken cancellationToken)
    {
        var existingRecords = await vectorStoreService.LoadAsync(cancellationToken);
        if (existingRecords.Count > 0)
        {
            logger.LogWarning("[INGEST] Rejected: vector store already contains {Count} records.", existingRecords.Count);
            return Conflict(new { error = "The knowledge base has already been ingested. Re-ingestion is not permitted." });
        }

        var configuredSourcePath = configuration["Challenge:SourceDocumentPath"] ?? "../../../../knowledge-base/Grocery_Store_SOP.md";

        logger.LogInformation("[INGEST] Configured: {ConfiguredPath} | ContentRoot: {ContentRoot}",
            configuration["Challenge:SourceDocumentPath"], env.ContentRootPath);

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
                return NotFound(new { error = "Source document not found." });
        }

        var sourceText = await System.IO.File.ReadAllTextAsync(sourcePath, cancellationToken);
        var sourceName = System.IO.Path.GetFileName(sourcePath);

        return await ProcessIngestAsync(sourceText, sourceName, sourcePath, cancellationToken);
    }

    // ── POST /api/v1/ingest/upload  (user-supplied file) ─────────────────────

    [HttpPost("upload")]
    [RequestSizeLimit(MaxUploadBytes)]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<IngestResponse>> Upload(IFormFile? file, CancellationToken cancellationToken)
    {
        var existingRecords = await vectorStoreService.LoadAsync(cancellationToken);
        if (existingRecords.Count > 0)
        {
            logger.LogWarning("[INGEST] Upload rejected: store already has {Count} records.", existingRecords.Count);
            return Conflict(new { error = "The knowledge base has already been ingested. Re-ingestion is not permitted." });
        }

        if (file is null || file.Length == 0)
            return BadRequest(new { error = "No file provided." });

        if (file.Length > MaxUploadBytes)
            return BadRequest(new { error = "File exceeds the 10 MB limit." });

        var ext = Path.GetExtension(file.FileName);
        if (!AllowedExtensions.Contains(ext))
            return BadRequest(new { error = "Only .md and .txt files are accepted." });

        // Use only the filename (never the path) to prevent path-traversal (OWASP A01).
        var sourceName = Path.GetFileName(file.FileName);

        string sourceText;
        using (var reader = new System.IO.StreamReader(file.OpenReadStream(), System.Text.Encoding.UTF8))
            sourceText = await reader.ReadToEndAsync(cancellationToken);

        logger.LogInformation("[INGEST] Upload received: {FileName} ({Bytes} bytes)", sourceName, file.Length);

        return await ProcessIngestAsync(sourceText, sourceName, sourceName, cancellationToken);
    }

    // ── Shared chunk → embed → persist pipeline ───────────────────────────────

    private async Task<ActionResult<IngestResponse>> ProcessIngestAsync(
        string sourceText, string sourceName, string displayPath, CancellationToken cancellationToken)
    {
        var chunks = await chunkingService.ChunkAsync(sourceText, sourceName, cancellationToken);
        var records = new List<VectorRecord>();

        foreach (var chunk in chunks)
        {
            var embedding = await embeddingService.EmbedAsync(chunk.Content, cancellationToken);
            records.Add(new VectorRecord
            {
                Id = chunk.Id,
                Source = chunk.Source,
                ChunkText = chunk.Content,
                Embedding = embedding,
                Metadata = new Dictionary<string, string>
                {
                    ["Index"] = chunk.Index.ToString()
                }
            });
        }

        await vectorStoreService.SaveAsync(records, cancellationToken);

        var vectorStorePath = configuration["Challenge:VectorStorePath"] ?? "Data/vector-store.json";

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
}
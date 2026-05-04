using Api.Contracts;
using Api.Models;
using Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class IngestController(
    IConfiguration configuration,
    IChunkingService chunkingService,
    IEmbeddingService embeddingService,
    IVectorStoreService vectorStoreService,
    ILogger<IngestController> logger) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<IngestResponse>> Post([FromBody] IngestRequest? request, CancellationToken cancellationToken, [FromServices] IWebHostEnvironment env)
    {
        var configuredSourcePath = configuration["Challenge:SourceDocumentPath"] ?? "../../../../knowledge-base/Grocery_Store_SOP.md";
        var requestedPath = string.IsNullOrWhiteSpace(request?.SourcePath) ? configuredSourcePath : request!.SourcePath;

        logger.LogInformation("[INGEST] Configured: {ConfiguredPath}", configuration["Challenge:SourceDocumentPath"]);
        logger.LogInformation("[INGEST] Requested: {RequestedPath}", requestedPath);
        logger.LogInformation("[INGEST] ContentRoot: {ContentRoot}", env.ContentRootPath);

        // Resolve path: check absolute, then check relative to content root (useful for Docker/Local mix)
        var sourcePath = Path.IsPathRooted(requestedPath)
            ? requestedPath
            : Path.GetFullPath(Path.Combine(env.ContentRootPath, requestedPath));

        logger.LogInformation("[INGEST] Resolved: {ResolvedPath}", sourcePath);

        if (!System.IO.File.Exists(sourcePath))
        {
            // Fallback for local development when running from /backend/src/Api
            var localFallback = Path.GetFullPath(Path.Combine(env.ContentRootPath, "../../../../knowledge-base/Grocery_Store_SOP.md"));
            logger.LogInformation("[INGEST] Fallback: {FallbackPath}", localFallback);
            if (System.IO.File.Exists(localFallback))
            {
                sourcePath = localFallback;
            }
            else
            {
                return NotFound(new { error = $"Source document not found. Looked at: {sourcePath} and {localFallback}" });
            }
        }

        var sourceText = await System.IO.File.ReadAllTextAsync(sourcePath, cancellationToken);
        var sourceName = System.IO.Path.GetFileName(sourcePath);

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
            Message = "SOP document ingested successfully.",
            SourcePath = sourcePath,
            ChunksCreated = chunks.Count,
            RecordsPersisted = records.Count,
            VectorStorePath = vectorStorePath,
            IsPlaceholder = false
        });
    }
}
using Api.Contracts;
using Api.Services;
using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
public sealed class HealthController(
    IConfiguration configuration,
    IVectorStoreService vectorStoreService,
    IWebHostEnvironment environment) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<HealthResponse>> Get(CancellationToken cancellationToken)
    {
        var hasApiKey = !string.IsNullOrWhiteSpace(configuration["OpenAI:ApiKey"]);

        var notes = hasApiKey
            ? new List<string>
            {
                "Service is fully operational.",
                "RAG Ingestion, JSON Vector Store, and Tool-calling are active.",
                "Resilience pipelines and strict validation are enabled."
            }
            : new List<string>
            {
                "Service is running in offline/fallback mode (no OpenAI API key).",
                "Using deterministic embeddings and keyword-based chat responses.",
                "Set OpenAI__ApiKey to enable full AI capabilities."
            };

        var records = await vectorStoreService.LoadAsync(cancellationToken);

        return Ok(new HealthResponse
        {
            Status = "ok",
            Service = "grocery-store-sop-assistant-api",
            UtcTime = DateTimeOffset.UtcNow,
            Notes = notes,
            IsIngested = records.Count > 0,
            RecordCount = records.Count
        });
    }

    [HttpGet("/api/v{version:apiVersion}/ready")]
    public ActionResult<HealthResponse> Ready()
    {
        var hasApiKey = !string.IsNullOrWhiteSpace(configuration["OpenAI:ApiKey"]);

        var sourceDocumentPath = ResolveSourceDocumentPath();
        var sourceDocumentReady = sourceDocumentPath is not null && System.IO.File.Exists(sourceDocumentPath);

        var vectorStoreReady = IsVectorStorePathWritable();

        var checks = new List<string>
        {
            sourceDocumentReady
                ? $"Source document available: {Path.GetFileName(sourceDocumentPath)}"
                : "Source document is missing.",
            vectorStoreReady
                ? "Vector store path is readable/writable."
                : "Vector store path is not readable/writable.",
            hasApiKey
                ? "AI mode: OpenAI"
                : "AI mode: Fallback (no OpenAI API key)"
        };

        var isReady = sourceDocumentReady && vectorStoreReady;

        var response = new HealthResponse
        {
            Status = isReady ? "ready" : "not_ready",
            Service = "grocery-store-sop-assistant-api",
            UtcTime = DateTimeOffset.UtcNow,
            Notes = checks,
            IsIngested = false,
            RecordCount = 0
        };

        return isReady ? Ok(response) : StatusCode(StatusCodes.Status503ServiceUnavailable, response);
    }

    private string? ResolveSourceDocumentPath()
    {
        var configuredSourcePath = configuration["Challenge:SourceDocumentPath"] ?? "../../../../knowledge-base/Grocery_Store_SOP.md";
        var sourcePath = Path.IsPathRooted(configuredSourcePath)
            ? configuredSourcePath
            : Path.GetFullPath(Path.Combine(environment.ContentRootPath, configuredSourcePath));

        if (System.IO.File.Exists(sourcePath))
            return sourcePath;

        var localFallback = Path.GetFullPath(Path.Combine(environment.ContentRootPath, "../../../../knowledge-base/Grocery_Store_SOP.md"));
        return System.IO.File.Exists(localFallback) ? localFallback : null;
    }

    private bool IsVectorStorePathWritable()
    {
        try
        {
            var configuredPath = configuration["Challenge:VectorStorePath"] ?? "Data/vector-store.json";
            var resolvedPath = Path.IsPathRooted(configuredPath)
                ? configuredPath
                : Path.Combine(environment.ContentRootPath, configuredPath);

            var directory = Path.GetDirectoryName(resolvedPath);
            if (string.IsNullOrWhiteSpace(directory))
                return false;

            Directory.CreateDirectory(directory);

            var probePath = Path.Combine(directory, ".ready_probe");
            System.IO.File.WriteAllText(probePath, "ok");
            System.IO.File.Delete(probePath);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
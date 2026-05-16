using Api.Contracts;
using Api.Models;
using Api.Options;
using Microsoft.Extensions.Options;

namespace Api.Application.Health;

public sealed class GetReadinessQueryHandler(
    IOptions<OpenAIOptions> openAiOptions,
    IOptions<ChallengeOptions> challengeOptions,
    IWebHostEnvironment environment)
{
    public HealthResponse Handle(GetReadinessQuery query)
    {
        var hasApiKey = !string.IsNullOrWhiteSpace(openAiOptions.Value.ApiKey);

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
                : "AI mode: Fallback (no OpenAI API key)",
            $"Default knowledge base: {KnowledgeBaseScope.DefaultKnowledgeBaseId}"
        };

        var isReady = sourceDocumentReady && vectorStoreReady;

        return new HealthResponse
        {
            Status = isReady ? "ready" : "not_ready",
            Service = "grocery-store-sop-assistant-api",
            UtcTime = DateTimeOffset.UtcNow,
            Notes = checks,
            IsIngested = false,
            RecordCount = 0,
            ActiveKnowledgeBaseIds = [KnowledgeBaseScope.DefaultKnowledgeBaseId]
        };
    }

    private string? ResolveSourceDocumentPath()
    {
        var configuredSourcePath = challengeOptions.Value.SourceDocumentPath;
        var sourcePath = Path.IsPathRooted(configuredSourcePath)
            ? configuredSourcePath
            : Path.GetFullPath(Path.Combine(environment.ContentRootPath, configuredSourcePath));

        if (System.IO.File.Exists(sourcePath))
        {
            return sourcePath;
        }

        var localFallback = Path.GetFullPath(Path.Combine(environment.ContentRootPath, "../../../../knowledge-base/Grocery_Store_SOP.md"));
        return System.IO.File.Exists(localFallback) ? localFallback : null;
    }

    private bool IsVectorStorePathWritable()
    {
        try
        {
            var configuredPath = challengeOptions.Value.VectorStorePath;
            var resolvedPath = Path.IsPathRooted(configuredPath)
                ? configuredPath
                : Path.Combine(environment.ContentRootPath, configuredPath);

            var directory = Path.GetDirectoryName(resolvedPath);
            if (string.IsNullOrWhiteSpace(directory))
            {
                return false;
            }

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

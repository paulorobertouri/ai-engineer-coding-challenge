using Api.Contracts;
using Api.Models;
using Api.Options;
using Microsoft.Extensions.Options;
using System.Net.Sockets;

namespace Api.Application.Health;

public sealed class GetReadinessQueryHandler(
    IOptions<OpenAIOptions> openAiOptions,
    IOptions<ChallengeOptions> challengeOptions,
    IOptions<VectorStoreOptions> vectorStoreOptions,
    IOptions<HealthChecksOptions> healthChecksOptions,
    IWebHostEnvironment environment)
{
    public async Task<HealthResponse> HandleAsync(GetReadinessQuery query, CancellationToken cancellationToken)
    {
        _ = query;

        var hasApiKey = !string.IsNullOrWhiteSpace(openAiOptions.Value.ApiKey);
        var provider = vectorStoreOptions.Value.Provider.Trim();
        var openAiConfigured = !hasApiKey
            || (!string.IsNullOrWhiteSpace(openAiOptions.Value.ChatModel)
                && !string.IsNullOrWhiteSpace(openAiOptions.Value.EmbeddingModel));

        var sourceDocumentPath = ResolveSourceDocumentPath();
        var sourceDocumentReady = sourceDocumentPath is not null && System.IO.File.Exists(sourceDocumentPath);

        var vectorStoreReady = IsVectorStorePathWritable();

        var providerConnectivityResult = await CheckProviderConnectivityAsync(
            hasApiKey,
            healthChecksOptions.Value,
            cancellationToken);

        var checks = new List<string>
        {
            sourceDocumentReady
                ? $"Source document available: {Path.GetFileName(sourceDocumentPath)}"
                : "Source document is missing.",
            vectorStoreReady
                ? "Vector store path is readable/writable."
                : "Vector store path is not readable/writable.",
            openAiConfigured
                ? "Selected AI mode configuration is valid."
                : "Selected AI mode configuration is invalid.",
            hasApiKey
                ? "AI mode: OpenAI"
                : "AI mode: Fallback (no OpenAI API key)",
            $"Vector store provider: {provider}",
            providerConnectivityResult,
            $"Default knowledge base: {KnowledgeBaseScope.DefaultKnowledgeBaseId}"
        };

        var connectivityReady = !providerConnectivityResult.StartsWith("OpenAI provider connectivity check failed", StringComparison.Ordinal);
        var isReady = sourceDocumentReady && vectorStoreReady && openAiConfigured && connectivityReady;

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

    private static async Task<string> CheckProviderConnectivityAsync(
        bool hasApiKey,
        HealthChecksOptions options,
        CancellationToken cancellationToken)
    {
        if (!hasApiKey)
        {
            return "OpenAI provider connectivity check skipped (fallback mode).";
        }

        if (!options.EnableOpenAIConnectivityProbe)
        {
            return "OpenAI provider connectivity check skipped (disabled by configuration).";
        }

        try
        {
            using var tcpClient = new TcpClient();
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(options.OpenAIProbeTimeoutMilliseconds);
            await tcpClient.ConnectAsync(options.OpenAIProbeHost, 443, timeoutCts.Token);
            return "OpenAI provider connectivity check passed.";
        }
        catch (Exception)
        {
            return "OpenAI provider connectivity check failed.";
        }
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

using Api.Contracts;
using Api.Models;
using Api.Options;
using Api.Services;
using Microsoft.Extensions.Options;

namespace Api.Application.Health;

public sealed class GetHealthQueryHandler(
    IOptions<OpenAIOptions> openAiOptions,
    IVectorStoreService vectorStoreService)
{
    public async Task<HealthResponse> HandleAsync(GetHealthQuery query, CancellationToken cancellationToken)
    {
        _ = query;

        var hasApiKey = !string.IsNullOrWhiteSpace(openAiOptions.Value.ApiKey);
        var records = await vectorStoreService.LoadAsync(cancellationToken);
        var normalizedKnowledgeBaseId = KnowledgeBaseScope.DefaultKnowledgeBaseId;
        var knowledgeBaseRecords = records
            .Where(record => KnowledgeBaseScope.BelongsToKnowledgeBase(record, normalizedKnowledgeBaseId))
            .ToList();

        var notes = hasApiKey
            ? new List<string>
            {
                "Service is fully operational.",
                "AI mode: OpenAI.",
                "Use /api/v1/ready for dependency and provider readiness details."
            }
            : new List<string>
            {
                "Service is running in offline/fallback mode (no OpenAI API key).",
                "Using deterministic embeddings and keyword-based chat responses.",
                "Use /api/v1/ready for dependency readiness details."
            };

        var activeKnowledgeBases = new List<string> { KnowledgeBaseScope.DefaultKnowledgeBaseId };

        return new HealthResponse
        {
            Status = "ok",
            Service = "grocery-store-sop-assistant-api",
            UtcTime = DateTimeOffset.UtcNow,
            Notes = notes,
            IsIngested = knowledgeBaseRecords.Count > 0,
            RecordCount = knowledgeBaseRecords.Count,
            ActiveKnowledgeBaseIds = activeKnowledgeBases
        };
    }
}

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
        var hasApiKey = !string.IsNullOrWhiteSpace(openAiOptions.Value.ApiKey);

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
        var activeKnowledgeBases = records
            .Select(KnowledgeBaseScope.GetRecordKnowledgeBaseId)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
            .ToList();

        notes.Add(activeKnowledgeBases.Count == 0
            ? $"Active knowledge bases: none (default: {KnowledgeBaseScope.DefaultKnowledgeBaseId})."
            : $"Active knowledge bases: {string.Join(", ", activeKnowledgeBases)}.");

        return new HealthResponse
        {
            Status = "ok",
            Service = "grocery-store-sop-assistant-api",
            UtcTime = DateTimeOffset.UtcNow,
            Notes = notes,
            IsIngested = records.Count > 0,
            RecordCount = records.Count,
            ActiveKnowledgeBaseIds = activeKnowledgeBases
        };
    }
}

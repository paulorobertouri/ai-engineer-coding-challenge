using Api.Contracts;
using Api.Models;
using Api.Options;
using Microsoft.Extensions.Options;

namespace Api.Application.Health;

public sealed class GetHealthQueryHandler(
    IOptions<OpenAIOptions> openAiOptions)
{
    public Task<HealthResponse> HandleAsync(GetHealthQuery query, CancellationToken cancellationToken)
    {
        _ = query;
        _ = cancellationToken;

        var hasApiKey = !string.IsNullOrWhiteSpace(openAiOptions.Value.ApiKey);

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

        return Task.FromResult(new HealthResponse
        {
            Status = "ok",
            Service = "grocery-store-sop-assistant-api",
            UtcTime = DateTimeOffset.UtcNow,
            Notes = notes,
            IsIngested = false,
            RecordCount = 0,
            ActiveKnowledgeBaseIds = activeKnowledgeBases
        });
    }
}

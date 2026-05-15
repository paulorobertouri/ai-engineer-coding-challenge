using Api.Observability;

namespace Api.Services;

public sealed class OpenAIRetrievalChatServiceDependencies(
    IEmbeddingService embeddingService,
    IVectorStoreService vectorStoreService,
    IRetrievalReranker reranker,
    IUserQueryGuardrailService guardrailService,
    OpenAIUsageTracker usageTracker,
    ILogger<OpenAIRetrievalChatService> logger)
{
    public IEmbeddingService EmbeddingService { get; } = embeddingService;
    public IVectorStoreService VectorStoreService { get; } = vectorStoreService;
    public IRetrievalReranker Reranker { get; } = reranker;
    public IUserQueryGuardrailService GuardrailService { get; } = guardrailService;
    public OpenAIUsageTracker UsageTracker { get; } = usageTracker;
    public ILogger<OpenAIRetrievalChatService> Logger { get; } = logger;
}

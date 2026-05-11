using OpenAI;
using OpenAI.Embeddings;

namespace Api.Services;

public sealed class OpenAIEmbeddingService(OpenAIClient openAiClient, IConfiguration configuration) : IEmbeddingService
{
    private const int DefaultEmbeddingDimensions = 1536;
    private readonly string _model = configuration["OpenAI:EmbeddingModel"] ?? "text-embedding-3-small";

    public async Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new float[DefaultEmbeddingDimensions];
        }

        var client = openAiClient.GetEmbeddingClient(_model);
        var response = await client.GenerateEmbeddingAsync(text, cancellationToken: cancellationToken);
        return response.Value.ToFloats().ToArray();
    }
}
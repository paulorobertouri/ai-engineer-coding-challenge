using Api.Models;

namespace Api.Services;

public interface IRetrievalReranker
{
    string Name { get; }

    IReadOnlyList<VectorSearchMatch> Rerank(
        string query,
        IReadOnlyList<VectorSearchMatch> candidates,
        int take);
}

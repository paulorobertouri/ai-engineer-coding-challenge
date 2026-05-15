using Api.Contracts;

namespace Api.Services;

public interface ISourceDocumentViewerService
{
    Task<SourceDocumentResponse?> GetDocumentAsync(
        string source,
        string? knowledgeBaseId,
        CancellationToken cancellationToken = default);
}

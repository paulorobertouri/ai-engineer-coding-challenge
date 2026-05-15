namespace Api.Services;

public interface IDocumentOcrService
{
    bool CanProcess(string extension);

    Task<string> ExtractTextAsync(string fileName, Stream stream, CancellationToken cancellationToken = default);
}

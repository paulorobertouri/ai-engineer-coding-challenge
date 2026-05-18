namespace Api.Services;

public interface IDocumentExtractionService
{
    bool IsSupportedExtension(string extension);

    string DescribeSupportedFormats();

    Task<string> ExtractTextFromFileAsync(string filePath, CancellationToken cancellationToken = default);

    Task<string> ExtractTextAsync(string fileName, Stream stream, CancellationToken cancellationToken = default);
}

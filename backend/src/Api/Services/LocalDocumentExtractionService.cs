using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using UglyToad.PdfPig;

namespace Api.Services;

public sealed class LocalDocumentExtractionService(IEnumerable<IDocumentOcrService> ocrServices) : IDocumentExtractionService
{
    private static readonly HashSet<string> BaselineExtensions =
        new([".md", ".txt", ".pdf", ".docx"], StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> OcrExtensions =
        new([".png", ".jpg", ".jpeg", ".tif", ".tiff", ".bmp"], StringComparer.OrdinalIgnoreCase);

    private readonly List<IDocumentOcrService> _ocrServices = ocrServices.ToList();

    public bool IsSupportedExtension(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
        {
            return false;
        }

        return BaselineExtensions.Contains(extension) || OcrExtensions.Contains(extension);
    }

    public string DescribeSupportedFormats()
    {
        return ".md, .txt, .pdf, .docx (OCR-based scanned files are optional: .png, .jpg, .jpeg, .tif, .tiff, .bmp)";
    }

    public async Task<string> ExtractTextFromFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var fileName = Path.GetFileName(filePath);
        await using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return await ExtractTextAsync(fileName, fileStream, cancellationToken);
    }

    public async Task<string> ExtractTextAsync(string fileName, Stream stream, CancellationToken cancellationToken = default)
    {
        var extension = Path.GetExtension(fileName);
        if (!IsSupportedExtension(extension))
        {
            throw new DocumentExtractionException($"Unsupported document format '{extension}'. Supported formats: {DescribeSupportedFormats()}");
        }

        cancellationToken.ThrowIfCancellationRequested();

        return extension.ToLowerInvariant() switch
        {
            ".md" or ".txt" => await ReadTextAsync(stream, cancellationToken),
            ".docx" => await ExtractDocxAsync(stream, cancellationToken),
            ".pdf" => await ExtractPdfAsync(stream, cancellationToken),
            _ => await ExtractWithOcrAsync(fileName, extension, stream, cancellationToken)
        };
    }

    private static async Task<string> ReadTextAsync(Stream stream, CancellationToken cancellationToken)
    {
        if (stream.CanSeek)
        {
            stream.Seek(0, SeekOrigin.Begin);
        }

        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        return await reader.ReadToEndAsync(cancellationToken);
    }

    private static async Task<string> ExtractDocxAsync(Stream stream, CancellationToken cancellationToken)
    {
        await using var workingStream = await CopyToSeekableStreamAsync(stream, cancellationToken);
        using var archive = new ZipArchive(workingStream, ZipArchiveMode.Read, leaveOpen: false);
        var entry = archive.GetEntry("word/document.xml");
        if (entry is null)
        {
            throw new DocumentExtractionException("The DOCX file is missing word/document.xml.");
        }

        await using var entryStream = entry.Open();
        var xml = await XDocument.LoadAsync(entryStream, LoadOptions.None, cancellationToken);
        XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        var paragraphs = xml
            .Descendants(w + "p")
            .Select(p => string.Concat(p.Descendants(w + "t").Select(t => t.Value)).Trim())
            .Where(text => !string.IsNullOrWhiteSpace(text));

        return string.Join(Environment.NewLine, paragraphs);
    }

    private static async Task<string> ExtractPdfAsync(Stream stream, CancellationToken cancellationToken)
    {
        await using var workingStream = await CopyToSeekableStreamAsync(stream, cancellationToken);
        using var document = PdfDocument.Open(workingStream);
        var builder = new StringBuilder();

        foreach (var page in document.GetPages())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!string.IsNullOrWhiteSpace(page.Text))
            {
                if (builder.Length > 0)
                {
                    builder.AppendLine();
                    builder.AppendLine();
                }

                builder.Append(page.Text.Trim());
            }
        }

        return builder.ToString();
    }

    private async Task<string> ExtractWithOcrAsync(
        string fileName,
        string extension,
        Stream stream,
        CancellationToken cancellationToken)
    {
        var ocrService = _ocrServices.FirstOrDefault(service => service.CanProcess(extension));
        if (ocrService is null)
        {
            throw new DocumentExtractionException(
                $"OCR processor is not configured for '{extension}'. Install and register a local OCR adapter to process scanned files.");
        }

        await using var workingStream = await CopyToSeekableStreamAsync(stream, cancellationToken);
        return await ocrService.ExtractTextAsync(fileName, workingStream, cancellationToken);
    }

    private static async Task<MemoryStream> CopyToSeekableStreamAsync(Stream stream, CancellationToken cancellationToken)
    {
        if (stream.CanSeek)
        {
            stream.Seek(0, SeekOrigin.Begin);
        }

        var memory = new MemoryStream();
        await stream.CopyToAsync(memory, cancellationToken);
        memory.Seek(0, SeekOrigin.Begin);
        return memory;
    }
}

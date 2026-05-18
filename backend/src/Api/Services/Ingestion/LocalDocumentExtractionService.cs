using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using UglyToad.PdfPig;

namespace Api.Services;

public sealed class LocalDocumentExtractionService(IEnumerable<IDocumentOcrService> ocrServices) : IDocumentExtractionService
{
    private const int SignatureProbeLength = 32;
    private const int TextProbeLength = 4096;
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

        await ValidateUploadedContentAsync(fileName, extension, stream, cancellationToken);

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

    private static async Task ValidateUploadedContentAsync(
        string fileName,
        string extension,
        Stream stream,
        CancellationToken cancellationToken)
    {
        var lowerExtension = extension.ToLowerInvariant();

        switch (lowerExtension)
        {
            case ".pdf":
                await EnsurePdfSignatureAsync(fileName, stream, cancellationToken);
                return;
            case ".docx":
                await EnsureZipSignatureAsync(fileName, stream, cancellationToken);
                return;
            case ".md":
            case ".txt":
                await EnsureReadableTextAsync(fileName, stream, cancellationToken);
                return;
            default:
                return;
        }
    }

    private static async Task EnsurePdfSignatureAsync(string fileName, Stream stream, CancellationToken cancellationToken)
    {
        var header = await ReadHeaderAsync(stream, SignatureProbeLength, cancellationToken);
        var pdfSignature = Encoding.ASCII.GetBytes("%PDF-");

        if (!header.AsSpan().StartsWith(pdfSignature))
        {
            throw new DocumentExtractionException(
                $"The uploaded file '{fileName}' is not a valid PDF document.");
        }
    }

    private static async Task EnsureZipSignatureAsync(string fileName, Stream stream, CancellationToken cancellationToken)
    {
        var header = await ReadHeaderAsync(stream, SignatureProbeLength, cancellationToken);
        var isZip = header.Length >= 4
            && header[0] == 0x50
            && header[1] == 0x4B
            && (header[2] == 0x03 || header[2] == 0x05 || header[2] == 0x07)
            && (header[3] == 0x04 || header[3] == 0x06 || header[3] == 0x08);

        if (!isZip)
        {
            throw new DocumentExtractionException(
                $"The uploaded file '{fileName}' is not a valid DOCX document.");
        }
    }

    private static async Task EnsureReadableTextAsync(string fileName, Stream stream, CancellationToken cancellationToken)
    {
        var probe = await ReadHeaderAsync(stream, TextProbeLength, cancellationToken);
        if (probe.Length == 0)
        {
            return;
        }

        if (probe.Any(b => b == 0x00))
        {
            throw new DocumentExtractionException(
                $"The uploaded file '{fileName}' appears to be binary and cannot be ingested as text.");
        }

        var suspiciousControlBytes = probe.Count(static b => b < 0x09 || (b > 0x0D && b < 0x20));
        if ((double)suspiciousControlBytes / probe.Length > 0.1)
        {
            throw new DocumentExtractionException(
                $"The uploaded file '{fileName}' appears to contain binary/control-byte content.");
        }
    }

    private static async Task<byte[]> ReadHeaderAsync(Stream stream, int maxBytes, CancellationToken cancellationToken)
    {
        if (stream.CanSeek)
        {
            stream.Seek(0, SeekOrigin.Begin);
        }

        var buffer = new byte[maxBytes];
        var read = await stream.ReadAsync(buffer.AsMemory(0, maxBytes), cancellationToken);

        if (stream.CanSeek)
        {
            stream.Seek(0, SeekOrigin.Begin);
        }

        return buffer[..read];
    }
}

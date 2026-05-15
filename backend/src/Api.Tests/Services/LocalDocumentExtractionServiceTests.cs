using Api.Services;
using System.IO.Compression;
using System.Text;
using Xunit;

namespace Api.Tests;

public class LocalDocumentExtractionServiceTests
{
    [Fact]
    public async Task ExtractTextAsync_Txt_ReturnsText()
    {
        var service = new LocalDocumentExtractionService([]);
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("Line 1\nLine 2"));

        var result = await service.ExtractTextAsync("notes.txt", stream, CancellationToken.None);

        Assert.Equal("Line 1\nLine 2", result);
    }

    [Fact]
    public async Task ExtractTextAsync_Docx_ReturnsParagraphText()
    {
        var service = new LocalDocumentExtractionService([]);
        await using var stream = CreateDocxStream("Hello from docx", "Second paragraph");

        var result = await service.ExtractTextAsync("manual.docx", stream, CancellationToken.None);

        Assert.Contains("Hello from docx", result, StringComparison.Ordinal);
        Assert.Contains("Second paragraph", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExtractTextAsync_ScannedWithoutOcr_ThrowsDocumentExtractionException()
    {
        var service = new LocalDocumentExtractionService([]);
        await using var stream = new MemoryStream([1, 2, 3]);

        var ex = await Assert.ThrowsAsync<DocumentExtractionException>(() =>
            service.ExtractTextAsync("scan.png", stream, CancellationToken.None));

        Assert.Contains("OCR processor is not configured", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void IsSupportedExtension_KnowsBaselineAndOcrFormats()
    {
        var service = new LocalDocumentExtractionService([]);

        Assert.True(service.IsSupportedExtension(".md"));
        Assert.True(service.IsSupportedExtension(".pdf"));
        Assert.True(service.IsSupportedExtension(".docx"));
        Assert.True(service.IsSupportedExtension(".png"));
        Assert.False(service.IsSupportedExtension(".exe"));
    }

    private static MemoryStream CreateDocxStream(string firstParagraph, string secondParagraph)
    {
        var memory = new MemoryStream();
        using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry("word/document.xml");
            using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
            writer.Write($"""
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main">
  <w:body>
    <w:p><w:r><w:t>{firstParagraph}</w:t></w:r></w:p>
    <w:p><w:r><w:t>{secondParagraph}</w:t></w:r></w:p>
  </w:body>
</w:document>
""");
        }

        memory.Seek(0, SeekOrigin.Begin);
        return memory;
    }
}

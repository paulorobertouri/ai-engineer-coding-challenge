using Api.Contracts;
using Api.Controllers;
using Api.Services;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace Api.Tests;

public sealed class SourcesControllerTests
{
    [Fact]
    public async Task GetDocument_MissingSource_ReturnsBadRequest()
    {
        var service = new Mock<ISourceDocumentViewerService>();
        var controller = new SourcesController(service.Object);

        var result = await controller.GetDocument("", "default", CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
        service.Verify(
            dependency => dependency.GetDocumentAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetDocument_NotFound_ReturnsNotFound()
    {
        var service = new Mock<ISourceDocumentViewerService>();
        service
            .Setup(dependency => dependency.GetDocumentAsync("Grocery_Store_SOP.md", "default", It.IsAny<CancellationToken>()))
            .ReturnsAsync((SourceDocumentResponse?)null);

        var controller = new SourcesController(service.Object);

        var result = await controller.GetDocument("Grocery_Store_SOP.md", "default", CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetDocument_Found_ReturnsOkDocument()
    {
        var expected = new SourceDocumentResponse
        {
            Source = "Grocery_Store_SOP.md",
            KnowledgeBaseId = "default",
            Chunks =
            [
                new SourceDocumentChunkDto
                {
                    ChunkId = "chunk-1",
                    Content = "Open the store at 7am.",
                    StartLine = 10,
                    EndLine = 12,
                    Index = 1
                }
            ]
        };

        var service = new Mock<ISourceDocumentViewerService>();
        service
            .Setup(dependency => dependency.GetDocumentAsync("Grocery_Store_SOP.md", "default", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var controller = new SourcesController(service.Object);

        var result = await controller.GetDocument("Grocery_Store_SOP.md", "default", CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<SourceDocumentResponse>(ok.Value);
        Assert.Equal("Grocery_Store_SOP.md", payload.Source);
        Assert.Single(payload.Chunks);
    }
}

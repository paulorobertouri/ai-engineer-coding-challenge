using Api.Application.Ingest;
using Api.Models;
using Api.Options;
using Api.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Api.Tests.Application;

public sealed class ResetKnowledgeBaseHandlerTests
{
    [Fact]
    public async Task HandleAsync_WhenConfirmationMatches_ResetsVectorStore()
    {
        var env = new Mock<IWebHostEnvironment>();
        env.Setup(e => e.EnvironmentName).Returns("Development");

        var vectorStore = new Mock<IVectorStoreService>();
        vectorStore
            .Setup(s => s.LoadAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new VectorRecord { Id = "1", Source = "SOP.md", ChunkText = "x", Embedding = [] }
            ]);
        vectorStore
            .Setup(s => s.SaveAsync(It.IsAny<IEnumerable<VectorRecord>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var options = Microsoft.Extensions.Options.Options.Create(new ChallengeOptions { VectorStorePath = "Data/vector-store.json" });
        var handler = new ResetKnowledgeBaseHandler(env.Object, vectorStore.Object, options, NullLogger.Instance);

        var result = await handler.HandleAsync(
            new ResetKnowledgeBaseCommand(ResetKnowledgeBaseHandler.RequiredConfirmation),
            CancellationToken.None);

        Assert.IsType<OkObjectResult>(result.Result);
        vectorStore.Verify(s => s.SaveAsync(It.IsAny<IEnumerable<VectorRecord>>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}

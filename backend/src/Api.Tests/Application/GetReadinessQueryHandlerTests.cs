using Api.Application.Health;
using Api.Options;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Api.Tests.Application;

public sealed class GetReadinessQueryHandlerTests
{
    [Fact]
    public void Handle_WhenSourceDocumentMissing_ReturnsNotReady()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"readiness-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var openAiOptions = Microsoft.Extensions.Options.Options.Create(new OpenAIOptions { ApiKey = string.Empty });
            var challengeOptions = Microsoft.Extensions.Options.Options.Create(new ChallengeOptions
            {
                SourceDocumentPath = Path.Combine(tempDir, "missing.md"),
                VectorStorePath = "Data/vector-store.json"
            });

            var env = new Mock<IWebHostEnvironment>();
            env.SetupGet(e => e.ContentRootPath).Returns(tempDir);

            var handler = new GetReadinessQueryHandler(openAiOptions, challengeOptions, env.Object);
            var response = handler.Handle(new GetReadinessQuery());

            Assert.Equal("not_ready", response.Status);
            Assert.Contains(response.Notes, n => n.Contains("Source document is missing", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }
}

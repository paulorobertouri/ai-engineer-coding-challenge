using Api.Application.Health;
using Api.Options;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Options;
using Moq;
using System.Net;
using System.Net.Sockets;
using Xunit;

namespace Api.Tests.Application;

public sealed class GetReadinessQueryHandlerTests
{
    [Fact]
    public async Task HandleAsync_WhenProbeDisabled_InOpenAiMode_RemainsReady()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"readiness-tests-probe-disabled-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var sourcePath = Path.Combine(tempDir, "SOP.md");
            await File.WriteAllTextAsync(sourcePath, "# SOP\ncontent");

            var openAiOptions = Microsoft.Extensions.Options.Options.Create(new OpenAIOptions
            {
                ApiKey = "test-key"
            });
            var challengeOptions = Microsoft.Extensions.Options.Options.Create(new ChallengeOptions
            {
                SourceDocumentPath = sourcePath,
                VectorStorePath = "Data/vector-store.json"
            });
            var vectorStoreOptions = Microsoft.Extensions.Options.Options.Create(new VectorStoreOptions
            {
                Provider = "json"
            });
            var healthChecksOptions = Microsoft.Extensions.Options.Options.Create(new HealthChecksOptions
            {
                EnableOpenAIConnectivityProbe = false,
                OpenAIProbeHost = "api.openai.com",
                OpenAIProbePort = 443,
                OpenAIProbeTimeoutMilliseconds = 1200
            });

            var env = new Mock<IWebHostEnvironment>();
            env.SetupGet(e => e.ContentRootPath).Returns(tempDir);

            var handler = new GetReadinessQueryHandler(openAiOptions, challengeOptions, vectorStoreOptions, healthChecksOptions, env.Object);
            var response = await handler.HandleAsync(new GetReadinessQuery(), CancellationToken.None);

            Assert.Equal("ready", response.Status);
            Assert.Contains(response.Notes, n => n.Contains("connectivity check skipped", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task HandleAsync_WhenProbeEnabledAndUnreachable_ReturnsNotReady()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"readiness-tests-probe-fail-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var sourcePath = Path.Combine(tempDir, "SOP.md");
            await File.WriteAllTextAsync(sourcePath, "# SOP\ncontent");

            var openAiOptions = Microsoft.Extensions.Options.Options.Create(new OpenAIOptions
            {
                ApiKey = "test-key"
            });
            var challengeOptions = Microsoft.Extensions.Options.Options.Create(new ChallengeOptions
            {
                SourceDocumentPath = sourcePath,
                VectorStorePath = "Data/vector-store.json"
            });
            var vectorStoreOptions = Microsoft.Extensions.Options.Options.Create(new VectorStoreOptions
            {
                Provider = "json"
            });
            var healthChecksOptions = Microsoft.Extensions.Options.Options.Create(new HealthChecksOptions
            {
                EnableOpenAIConnectivityProbe = true,
                OpenAIProbeHost = "127.0.0.1",
                OpenAIProbePort = 1,
                OpenAIProbeTimeoutMilliseconds = 300
            });

            var env = new Mock<IWebHostEnvironment>();
            env.SetupGet(e => e.ContentRootPath).Returns(tempDir);

            var handler = new GetReadinessQueryHandler(openAiOptions, challengeOptions, vectorStoreOptions, healthChecksOptions, env.Object);
            var response = await handler.HandleAsync(new GetReadinessQuery(), CancellationToken.None);

            Assert.Equal("not_ready", response.Status);
            Assert.Contains(response.Notes, n => n.Contains("connectivity check failed", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task HandleAsync_WhenProbeEnabledAndReachable_ReturnsReady()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"readiness-tests-probe-pass-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var probePort = ((IPEndPoint)listener.LocalEndpoint).Port;

        var acceptTask = Task.Run(async () =>
        {
            using var client = await listener.AcceptTcpClientAsync();
        });

        try
        {
            var sourcePath = Path.Combine(tempDir, "SOP.md");
            await File.WriteAllTextAsync(sourcePath, "# SOP\ncontent");

            var openAiOptions = Microsoft.Extensions.Options.Options.Create(new OpenAIOptions
            {
                ApiKey = "test-key"
            });
            var challengeOptions = Microsoft.Extensions.Options.Options.Create(new ChallengeOptions
            {
                SourceDocumentPath = sourcePath,
                VectorStorePath = "Data/vector-store.json"
            });
            var vectorStoreOptions = Microsoft.Extensions.Options.Options.Create(new VectorStoreOptions
            {
                Provider = "json"
            });
            var healthChecksOptions = Microsoft.Extensions.Options.Options.Create(new HealthChecksOptions
            {
                EnableOpenAIConnectivityProbe = true,
                OpenAIProbeHost = "127.0.0.1",
                OpenAIProbePort = probePort,
                OpenAIProbeTimeoutMilliseconds = 1200
            });

            var env = new Mock<IWebHostEnvironment>();
            env.SetupGet(e => e.ContentRootPath).Returns(tempDir);

            var handler = new GetReadinessQueryHandler(openAiOptions, challengeOptions, vectorStoreOptions, healthChecksOptions, env.Object);
            var response = await handler.HandleAsync(new GetReadinessQuery(), CancellationToken.None);

            Assert.Equal("ready", response.Status);
            Assert.Contains(response.Notes, n => n.Contains("connectivity check passed", StringComparison.OrdinalIgnoreCase));
            await acceptTask;
        }
        finally
        {
            listener.Stop();

            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task HandleAsync_WhenSourceDocumentMissing_ReturnsNotReady()
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
            var vectorStoreOptions = Microsoft.Extensions.Options.Options.Create(new VectorStoreOptions
            {
                Provider = "json"
            });
            var healthChecksOptions = Microsoft.Extensions.Options.Options.Create(new HealthChecksOptions
            {
                EnableOpenAIConnectivityProbe = false,
                OpenAIProbeHost = "api.openai.com",
                OpenAIProbeTimeoutMilliseconds = 1200
            });

            var env = new Mock<IWebHostEnvironment>();
            env.SetupGet(e => e.ContentRootPath).Returns(tempDir);

            var handler = new GetReadinessQueryHandler(openAiOptions, challengeOptions, vectorStoreOptions, healthChecksOptions, env.Object);
            var response = await handler.HandleAsync(new GetReadinessQuery(), CancellationToken.None);

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

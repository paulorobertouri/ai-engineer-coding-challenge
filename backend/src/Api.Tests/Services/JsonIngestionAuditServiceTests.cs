using Api.Contracts;
using Api.Options;
using Api.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using System.Text.Json;
using Xunit;

namespace Api.Tests;

public class JsonIngestionAuditServiceTests
{
    [Fact]
    public async Task RecordSuccessAsync_AppendsJsonRecord()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"audit-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var service = BuildService(tempDir);

            await service.RecordSuccessAsync(new IngestionAuditRecord
            {
                TimestampUtc = DateTimeOffset.Parse("2026-05-14T10:00:00+00:00"),
                Action = "default-ingest",
                KnowledgeBaseId = "default",
                SourceName = "SOP.md",
                SourceChecksum = "sha256:abc",
                DocumentVersion = "sha256:abc",
                ChunkCount = 3,
                RecordsPersisted = 3,
                TriggeredBy = "Development"
            });

            var auditPath = Path.Combine(tempDir, "Data", "ingestion-audit.json");
            var json = await File.ReadAllTextAsync(auditPath);
            using var document = JsonDocument.Parse(json);
            var entry = document.RootElement[0];

            Assert.Equal("success", entry.GetProperty("outcome").GetString());
            Assert.Equal("SOP.md", entry.GetProperty("sourceName").GetString());
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
    public async Task RecordFailureAsync_AppendsSafeSummary()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"audit-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var service = BuildService(tempDir);

            await service.RecordFailureAsync(new IngestionAuditRecord
            {
                TimestampUtc = DateTimeOffset.Parse("2026-05-14T10:00:00+00:00"),
                Action = "upload-ingest",
                KnowledgeBaseId = "store",
                SourceName = "upload.md",
                SafeSummary = "unsupported file type",
                TriggeredBy = "Development"
            });

            var auditPath = Path.Combine(tempDir, "Data", "ingestion-audit.json");
            var json = await File.ReadAllTextAsync(auditPath);
            using var document = JsonDocument.Parse(json);
            var entry = document.RootElement[0];

            Assert.Equal("failure", entry.GetProperty("outcome").GetString());
            Assert.Equal("unsupported file type", entry.GetProperty("safeSummary").GetString());
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    private static JsonIngestionAuditService BuildService(string contentRootPath)
    {
        var env = new StubWebHostEnvironment(contentRootPath);
        var options = Microsoft.Extensions.Options.Options.Create(new ChallengeOptions());
        return new JsonIngestionAuditService(options, env);
    }

    private sealed class StubWebHostEnvironment(string contentRootPath) : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "Api.Tests";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = string.Empty;
        public string ContentRootPath { get; set; } = contentRootPath;
        public string EnvironmentName { get; set; } = "Development";
    }
}

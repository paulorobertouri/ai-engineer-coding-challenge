using Api.Contracts;
using Api.Options;
using Api.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using System.Text.Json;
using Xunit;

namespace Api.Tests;

public class JsonConversationFeedbackServiceTests
{
    [Fact]
    public async Task RecordAsync_AppendsFeedbackRecord()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"feedback-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var service = BuildService(tempDir);

            await service.RecordAsync(new ConversationFeedbackRecord
            {
                TimestampUtc = DateTimeOffset.Parse("2026-05-15T10:00:00+00:00"),
                ConversationId = "conv-1",
                MessageId = "assistant-1",
                FeedbackType = "helpful",
                Comment = "clear answer"
            });

            var feedbackPath = Path.Combine(tempDir, "Data", "conversation-feedback.json");
            var json = await File.ReadAllTextAsync(feedbackPath);
            using var document = JsonDocument.Parse(json);
            var entry = document.RootElement[0];

            Assert.Equal("conv-1", entry.GetProperty("conversationId").GetString());
            Assert.Equal("assistant-1", entry.GetProperty("messageId").GetString());
            Assert.Equal("helpful", entry.GetProperty("feedbackType").GetString());
            Assert.Equal("clear answer", entry.GetProperty("comment").GetString());
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
    public async Task RecordAsync_UsesConfiguredPathRelativeToContentRoot()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"feedback-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var service = BuildService(tempDir);

            await service.RecordAsync(new ConversationFeedbackRecord
            {
                ConversationId = "conv-2",
                MessageId = "assistant-2",
                FeedbackType = "wrong-citation"
            });

            var feedbackPath = Path.Combine(tempDir, "Data", "conversation-feedback.json");
            Assert.True(File.Exists(feedbackPath));
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    private static JsonConversationFeedbackService BuildService(string contentRootPath)
    {
        var env = new StubWebHostEnvironment(contentRootPath);
        var options = Microsoft.Extensions.Options.Options.Create(new ChallengeOptions());
        return new JsonConversationFeedbackService(options, env);
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

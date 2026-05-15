using Api.Contracts;
using Api.Options;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace Api.Services;

public sealed class JsonConversationFeedbackService(IOptions<ChallengeOptions> options, IWebHostEnvironment environment)
    : IConversationFeedbackService
{
    private static readonly SemaphoreSlim FeedbackLock = new(1, 1);
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly string _feedbackPath = ResolveFeedbackPath(options.Value.ConversationFeedbackPath, environment.ContentRootPath);

    public async Task RecordAsync(ConversationFeedbackRecord record, CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(_feedbackPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await FeedbackLock.WaitAsync(cancellationToken);
        try
        {
            var records = await LoadExistingAsync(cancellationToken);
            records.Add(record with { TimestampUtc = record.TimestampUtc == default ? DateTimeOffset.UtcNow : record.TimestampUtc });
            var json = JsonSerializer.Serialize(records, SerializerOptions);
            await File.WriteAllTextAsync(_feedbackPath, json, cancellationToken);
        }
        finally
        {
            FeedbackLock.Release();
        }
    }

    private async Task<List<ConversationFeedbackRecord>> LoadExistingAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_feedbackPath))
        {
            return [];
        }

        var json = await File.ReadAllTextAsync(_feedbackPath, cancellationToken);
        return JsonSerializer.Deserialize<List<ConversationFeedbackRecord>>(json, SerializerOptions) ?? [];
    }

    private static string ResolveFeedbackPath(string configuredPath, string contentRootPath)
    {
        if (Path.IsPathRooted(configuredPath))
        {
            return configuredPath;
        }

        return Path.GetFullPath(Path.Combine(contentRootPath, configuredPath));
    }
}

using System.Text.Json;
using Api.Contracts;
using Api.Models;
using Api.Options;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Options;

namespace Api.Services;

public interface IRetrievalBenchmarkService
{
    Task<RetrievalBenchmarkEntryDto> RunAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RetrievalBenchmarkEntryDto>> ListAsync(int limit = 50, CancellationToken cancellationToken = default);
}

public sealed class RetrievalBenchmarkService(
    IVectorStoreService vectorStoreService,
    IOptions<RetrievalBenchmarkOptions> options,
    IWebHostEnvironment environment) : IRetrievalBenchmarkService
{
    private static readonly SemaphoreSlim HistoryLock = new(1, 1);
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly string _historyPath = ResolvePath(options.Value.HistoryPath, environment.ContentRootPath);
    private readonly int _maxHistoryEntries = options.Value.MaxHistoryEntries;

    private static readonly (string Query, string ExpectedTerm)[] Fixtures =
    [
        ("How often must checkout belts be sanitized?", "checkout lanes"),
        ("What is the hot foods minimum temperature?", "135"),
        ("How often should deli slicers be sanitized?", "slicer")
    ];

    public async Task<RetrievalBenchmarkEntryDto> RunAsync(CancellationToken cancellationToken = default)
    {
        var records = await vectorStoreService.LoadAsync(cancellationToken);
        var fixtureCount = Fixtures.Length;
        if (fixtureCount == 0 || records.Count == 0)
        {
            return await PersistAsync(new RetrievalBenchmarkEntryDto
            {
                RunId = Guid.NewGuid(),
                TimestampUtc = DateTimeOffset.UtcNow,
                Commit = ResolveCommitSha(),
                FixtureCount = fixtureCount,
                Precision = 0,
                Recall = 0
            }, cancellationToken);
        }

        var precisionTotal = 0d;
        var recallTotal = 0d;

        foreach (var fixture in Fixtures)
        {
            var topMatches = RankByLexicalOverlap(records, fixture.Query)
                .Take(5)
                .ToList();

            var relevantRetrieved = topMatches.Count(record => Contains(record.ChunkText, fixture.ExpectedTerm));
            var relevantCorpus = records.Count(record => Contains(record.ChunkText, fixture.ExpectedTerm));

            var precision = topMatches.Count == 0 ? 0 : (double)relevantRetrieved / topMatches.Count;
            var recallDenominator = Math.Max(1, Math.Min(5, relevantCorpus));
            var recall = (double)relevantRetrieved / recallDenominator;

            precisionTotal += precision;
            recallTotal += Math.Min(1.0, recall);
        }

        var entry = new RetrievalBenchmarkEntryDto
        {
            RunId = Guid.NewGuid(),
            TimestampUtc = DateTimeOffset.UtcNow,
            Commit = ResolveCommitSha(),
            FixtureCount = fixtureCount,
            Precision = precisionTotal / fixtureCount,
            Recall = recallTotal / fixtureCount
        };

        return await PersistAsync(entry, cancellationToken);
    }

    public async Task<IReadOnlyList<RetrievalBenchmarkEntryDto>> ListAsync(int limit = 50, CancellationToken cancellationToken = default)
    {
        var normalizedLimit = Math.Clamp(limit, 1, 200);

        await HistoryLock.WaitAsync(cancellationToken);
        try
        {
            var history = await LoadHistoryAsync(cancellationToken);
            return history
                .OrderByDescending(entry => entry.TimestampUtc)
                .Take(normalizedLimit)
                .ToList();
        }
        finally
        {
            HistoryLock.Release();
        }
    }

    private async Task<RetrievalBenchmarkEntryDto> PersistAsync(RetrievalBenchmarkEntryDto entry, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(_historyPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await HistoryLock.WaitAsync(cancellationToken);
        try
        {
            var history = await LoadHistoryAsync(cancellationToken);
            history.Add(entry);
            if (history.Count > _maxHistoryEntries)
            {
                history = history
                    .OrderByDescending(item => item.TimestampUtc)
                    .Take(_maxHistoryEntries)
                    .OrderBy(item => item.TimestampUtc)
                    .ToList();
            }

            var json = JsonSerializer.Serialize(history, SerializerOptions);
            await File.WriteAllTextAsync(_historyPath, json, cancellationToken);
            return entry;
        }
        finally
        {
            HistoryLock.Release();
        }
    }

    private async Task<List<RetrievalBenchmarkEntryDto>> LoadHistoryAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_historyPath))
        {
            return [];
        }

        var json = await File.ReadAllTextAsync(_historyPath, cancellationToken);
        return JsonSerializer.Deserialize<List<RetrievalBenchmarkEntryDto>>(json, SerializerOptions) ?? [];
    }

    private static IEnumerable<VectorRecord> RankByLexicalOverlap(IReadOnlyList<VectorRecord> records, string query)
    {
        var queryTokens = query
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(token => token.ToLowerInvariant())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return records
            .Select(record => new
            {
                Record = record,
                Score = queryTokens.Count(token => Contains(record.ChunkText, token))
            })
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Record.Id, StringComparer.Ordinal)
            .Select(item => item.Record);
    }

    private static bool Contains(string text, string term)
        => text.Contains(term, StringComparison.OrdinalIgnoreCase);

    private static string ResolveCommitSha()
    {
        var sha = Environment.GetEnvironmentVariable("GITHUB_SHA");
        return string.IsNullOrWhiteSpace(sha) ? "local" : sha;
    }

    private static string ResolvePath(string configuredPath, string contentRoot)
    {
        if (Path.IsPathRooted(configuredPath))
        {
            return configuredPath;
        }

        return Path.GetFullPath(Path.Combine(contentRoot, configuredPath));
    }
}

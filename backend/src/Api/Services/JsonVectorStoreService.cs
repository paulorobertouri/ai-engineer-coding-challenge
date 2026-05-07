using Api.Models;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Api.Services;

public sealed class JsonVectorStoreService : IVectorStoreService
{
    private readonly string _storePath;
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    // Protects concurrent writes and cache invalidation
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    // Cached records; null means the cache is cold (not yet loaded)
    private IReadOnlyList<VectorRecord>? _cachedRecords;

    public JsonVectorStoreService(IConfiguration configuration, IWebHostEnvironment environment)
    {
        var configuredPath = configuration["Challenge:VectorStorePath"] ?? "Data/vector-store.json";
        _storePath = Path.IsPathRooted(configuredPath)
            ? configuredPath
            : Path.Combine(environment.ContentRootPath, configuredPath);

        var directory = Path.GetDirectoryName(_storePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    public async Task<IReadOnlyList<VectorRecord>> LoadAsync(CancellationToken cancellationToken = default)
    {
        // Fast path: cache already populated (volatile read is safe for reference types on .NET)
        if (_cachedRecords is not null)
            return _cachedRecords;

        // Slow path: acquire the write lock and double-check to avoid reading stale data
        // that another thread may have just invalidated.
        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            if (_cachedRecords is not null)
                return _cachedRecords;

            if (!File.Exists(_storePath))
                return [];

            await using var stream = File.OpenRead(_storePath);
            var records = await JsonSerializer.DeserializeAsync<List<VectorRecord>>(stream, _jsonOptions, cancellationToken);
            _cachedRecords = records ?? [];
            return _cachedRecords;
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task SaveAsync(IEnumerable<VectorRecord> records, CancellationToken cancellationToken = default)
    {
        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            await using var stream = File.Open(_storePath, FileMode.Create, FileAccess.Write, FileShare.None);
            await JsonSerializer.SerializeAsync(stream, records, _jsonOptions, cancellationToken);
            // Invalidate cache so the next read picks up the fresh data
            _cachedRecords = null;
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task<IReadOnlyList<VectorSearchMatch>> SearchAsync(float[] queryEmbedding, int topK, CancellationToken cancellationToken = default)
    {
        var records = await LoadAsync(cancellationToken);

        return records
            .Select(r => new VectorSearchMatch
            {
                Record = r,
                Score = CosineSimilarity(queryEmbedding, r.Embedding)
            })
            .OrderByDescending(m => m.Score)
            .Take(topK)
            .ToList();
    }

    private static double CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length || a.Length == 0)
            return 0.0;

        double dot = 0, normA = 0, normB = 0;
        for (int i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }

        var denom = Math.Sqrt(normA) * Math.Sqrt(normB);
        return denom < 1e-10 ? 0.0 : dot / denom;
    }
}

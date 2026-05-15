using Api.Contracts;
using Api.Models;

namespace Api.Services;

public sealed class VectorStoreSourceDocumentViewerService(IVectorStoreService vectorStoreService) : ISourceDocumentViewerService
{
    public async Task<SourceDocumentResponse?> GetDocumentAsync(
        string source,
        string? knowledgeBaseId,
        CancellationToken cancellationToken = default)
    {
        var normalizedSource = Path.GetFileName(source.Trim());
        if (string.IsNullOrWhiteSpace(normalizedSource))
        {
            return null;
        }

        var normalizedKnowledgeBaseId = KnowledgeBaseScope.Normalize(knowledgeBaseId);
        var records = await vectorStoreService.LoadAsync(cancellationToken);
        var matchingRecords = records
            .Where(record =>
                KnowledgeBaseScope.BelongsToKnowledgeBase(record, normalizedKnowledgeBaseId)
                && string.Equals(record.Source, normalizedSource, StringComparison.OrdinalIgnoreCase))
            .Select(record => new
            {
                Record = record,
                Index = TryGetIntMetadata(record, "Index"),
                StartLine = TryGetIntMetadata(record, "StartLine"),
                EndLine = TryGetIntMetadata(record, "EndLine"),
                SectionTitle = TryGetStringMetadata(record, "SectionTitle"),
                DocumentVersion = TryGetStringMetadata(record, DocumentVersioning.DocumentVersionMetadataKey)
            })
            .OrderBy(item => item.Index ?? int.MaxValue)
            .ThenBy(item => item.StartLine ?? int.MaxValue)
            .ThenBy(item => item.Record.Id, StringComparer.Ordinal)
            .ToList();

        if (matchingRecords.Count == 0)
        {
            return null;
        }

        return new SourceDocumentResponse
        {
            Source = normalizedSource,
            KnowledgeBaseId = normalizedKnowledgeBaseId,
            DocumentVersion = matchingRecords
                .Select(item => item.DocumentVersion)
                .FirstOrDefault(version => !string.IsNullOrWhiteSpace(version)),
            Chunks = matchingRecords
                .Select(item => new SourceDocumentChunkDto
                {
                    ChunkId = item.Record.Id,
                    SectionTitle = item.SectionTitle ?? string.Empty,
                    Content = item.Record.ChunkText,
                    StartLine = item.StartLine,
                    EndLine = item.EndLine,
                    Index = item.Index
                })
                .ToList()
        };
    }

    private static int? TryGetIntMetadata(VectorRecord record, string key)
    {
        if (record.Metadata.TryGetValue(key, out var rawValue)
            && int.TryParse(rawValue, out var parsedValue))
        {
            return parsedValue;
        }

        return null;
    }

    private static string? TryGetStringMetadata(VectorRecord record, string key)
    {
        return record.Metadata.TryGetValue(key, out var rawValue)
            ? rawValue
            : null;
    }
}

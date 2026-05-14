namespace Api.Models;

public static class KnowledgeBaseScope
{
    public const string MetadataKey = "KnowledgeBaseId";
    public const string DefaultKnowledgeBaseId = "default";

    public static string Normalize(string? knowledgeBaseId)
    {
        if (string.IsNullOrWhiteSpace(knowledgeBaseId))
        {
            return DefaultKnowledgeBaseId;
        }

        return knowledgeBaseId.Trim().ToLowerInvariant();
    }

    public static string GetRecordKnowledgeBaseId(VectorRecord record)
    {
        if (!record.Metadata.TryGetValue(MetadataKey, out var value) || string.IsNullOrWhiteSpace(value))
        {
            // Backward compatibility: pre-task records are treated as the default knowledge base.
            return DefaultKnowledgeBaseId;
        }

        return Normalize(value);
    }

    public static bool BelongsToKnowledgeBase(VectorRecord record, string knowledgeBaseId)
    {
        return string.Equals(
            GetRecordKnowledgeBaseId(record),
            Normalize(knowledgeBaseId),
            StringComparison.OrdinalIgnoreCase);
    }

    public static IReadOnlyDictionary<string, string> BuildMetadataFilter(string? knowledgeBaseId)
    {
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [MetadataKey] = Normalize(knowledgeBaseId)
        };
    }
}

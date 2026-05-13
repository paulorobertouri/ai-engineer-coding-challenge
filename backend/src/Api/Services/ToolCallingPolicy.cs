using Api.Models;
using System.Text.Json;

namespace Api.Services;

public enum ToolCallQueryParseResult
{
    Success,
    InvalidJson,
    EmptyQuery
}

public static class ToolCallingPolicy
{
    public static bool IsEnabled(IConfiguration configuration) =>
        configuration.GetValue<bool?>("OpenAI:EnableTools") ?? true;

    public static ToolCallQueryParseResult TryExtractSearchQuery(string functionArguments, out string query)
    {
        query = string.Empty;
        try
        {
            var args = JsonDocument.Parse(functionArguments).RootElement;
            query = args.TryGetProperty("query", out var queryProp) ? queryProp.GetString() ?? string.Empty : string.Empty;
            return string.IsNullOrWhiteSpace(query)
                ? ToolCallQueryParseResult.EmptyQuery
                : ToolCallQueryParseResult.Success;
        }
        catch (JsonException)
        {
            return ToolCallQueryParseResult.InvalidJson;
        }
    }

    public static string BuildToolContext(IReadOnlyList<VectorSearchMatch> toolMatches) =>
        toolMatches.Count == 0
            ? string.Empty
            : string.Join("\n\n", toolMatches.Select(m => m.Record.ChunkText));
}
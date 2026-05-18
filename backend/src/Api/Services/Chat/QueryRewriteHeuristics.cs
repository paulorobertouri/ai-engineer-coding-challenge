using Api.Contracts;

namespace Api.Services;

internal static class QueryRewriteHeuristics
{
    private static readonly string[] FollowUpMarkers =
    [
        "what about",
        "and ",
        "also",
        "what if",
        "how about",
        "does that",
        "is it",
        "are they",
        "those",
        "them",
        "that",
        "it"
    ];

    public static (string Query, bool WasRewritten) Rewrite(
        IReadOnlyList<ChatMessageDto> messages,
        bool enabled)
    {
        var latestUserMessage = messages
            .LastOrDefault(m => m.Role.Equals("user", StringComparison.OrdinalIgnoreCase))?.Content
            ?.Trim() ?? string.Empty;

        if (!enabled || string.IsNullOrWhiteSpace(latestUserMessage))
        {
            return (latestUserMessage, false);
        }

        var previousUserMessage = messages
            .Where(m => m.Role.Equals("user", StringComparison.OrdinalIgnoreCase))
            .Select(m => m.Content.Trim())
            .Where(content => !string.IsNullOrWhiteSpace(content))
            .Reverse()
            .Skip(1)
            .FirstOrDefault();

        if (string.IsNullOrWhiteSpace(previousUserMessage) || !LooksLikeFollowUp(latestUserMessage))
        {
            return (latestUserMessage, false);
        }

        return ($"{previousUserMessage} Follow-up: {latestUserMessage}", true);
    }

    private static bool LooksLikeFollowUp(string latestUserMessage)
    {
        if (latestUserMessage.Length <= 28)
        {
            return true;
        }

        var normalized = latestUserMessage.Trim().ToLowerInvariant();
        return FollowUpMarkers.Any(marker => normalized.StartsWith(marker, StringComparison.Ordinal));
    }
}

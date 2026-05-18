using Api.Contracts;

namespace Api.Services;

internal static class StructuredAnswerFactory
{
    public static StructuredAnswerDto Create(
        string answerText,
        IEnumerable<string> citedChunkIds,
        string? refusalReason = null,
        IEnumerable<string>? followUpSuggestions = null)
    {
        var candidate = new StructuredAnswerDto
        {
            AnswerText = answerText,
            CitedChunkIds = citedChunkIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).ToList(),
            RefusalReason = string.IsNullOrWhiteSpace(refusalReason) ? null : refusalReason,
            FollowUpSuggestions = followUpSuggestions?.Where(s => !string.IsNullOrWhiteSpace(s)).Distinct(StringComparer.Ordinal).ToList() ?? []
        };

        return ValidateOrFallback(candidate, answerText, citedChunkIds);
    }

    public static StructuredAnswerDto ValidateOrFallback(
        StructuredAnswerDto candidate,
        string fallbackAnswerText,
        IEnumerable<string> fallbackCitedChunkIds)
    {
        if (string.IsNullOrWhiteSpace(candidate.AnswerText))
        {
            return new StructuredAnswerDto
            {
                AnswerText = fallbackAnswerText,
                CitedChunkIds = fallbackCitedChunkIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).ToList(),
                FollowUpSuggestions = []
            };
        }

        if (candidate.CitedChunkIds.Any(string.IsNullOrWhiteSpace))
        {
            return new StructuredAnswerDto
            {
                AnswerText = candidate.AnswerText,
                CitedChunkIds = fallbackCitedChunkIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).ToList(),
                RefusalReason = candidate.RefusalReason,
                FollowUpSuggestions = candidate.FollowUpSuggestions
            };
        }

        return candidate;
    }
}

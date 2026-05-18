using System.Text.RegularExpressions;

namespace Api.Services;

public static partial class SensitiveDataRedactor
{
    public static string? Sanitize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        var sanitized = value;
        sanitized = EmailRegex().Replace(sanitized, "[redacted:email]");
        sanitized = OpenAiKeyRegex().Replace(sanitized, "[redacted:api-key]");
        sanitized = BearerTokenRegex().Replace(sanitized, "Bearer [redacted:token]");
        sanitized = CardLikeDigitsRegex().Replace(sanitized, "[redacted:card]");
        return sanitized;
    }

    [GeneratedRegex(@"\b[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}\b", RegexOptions.IgnoreCase)]
    private static partial Regex EmailRegex();

    [GeneratedRegex(@"\bsk-[a-zA-Z0-9]{20,}\b", RegexOptions.IgnoreCase)]
    private static partial Regex OpenAiKeyRegex();

    [GeneratedRegex(@"Bearer\s+[A-Za-z0-9\-._~+/]+=*", RegexOptions.IgnoreCase)]
    private static partial Regex BearerTokenRegex();

    [GeneratedRegex(@"\b(?:\d[ -]*?){13,19}\b")]
    private static partial Regex CardLikeDigitsRegex();
}

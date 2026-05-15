using System.ComponentModel.DataAnnotations;

namespace Api.Options;

public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    [Required]
    [MinLength(1)]
    public string ApiKeyHeaderName { get; init; } = "X-Api-Key";

    public string? ApiKey { get; init; }

    public IReadOnlyList<AuthApiKeyOptions> ApiKeys { get; init; } = [];

    public bool AllowAnonymousInDevelopment { get; init; } = true;
}

public sealed class AuthApiKeyOptions
{
    [Required]
    [MinLength(1)]
    public string Name { get; init; } = string.Empty;

    [Required]
    [MinLength(1)]
    public string Key { get; init; } = string.Empty;

    public IReadOnlyList<string> Scopes { get; init; } = [];
}

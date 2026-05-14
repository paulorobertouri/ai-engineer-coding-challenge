using Microsoft.Extensions.Options;

namespace Api.Options;

public sealed class VectorStoreOptionsValidator : IValidateOptions<VectorStoreOptions>
{
    private static readonly string[] SupportedProviders = ["json"];

    public ValidateOptionsResult Validate(string? name, VectorStoreOptions options)
    {
        var provider = options.Provider?.Trim().ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(provider))
        {
            return ValidateOptionsResult.Fail("VectorStore:Provider is required.");
        }

        if (!SupportedProviders.Contains(provider, StringComparer.OrdinalIgnoreCase))
        {
            return ValidateOptionsResult.Fail(
                $"Unsupported VectorStore provider '{options.Provider}'. Supported providers: {string.Join(", ", SupportedProviders)}.");
        }

        return ValidateOptionsResult.Success;
    }
}

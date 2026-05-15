using Api.Options;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace Api.Security;

public sealed class LocalApiKeyAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> schemeOptions,
    ILoggerFactory loggerFactory,
    UrlEncoder encoder,
    IOptions<AuthOptions> authOptions,
    IWebHostEnvironment environment)
    : AuthenticationHandler<AuthenticationSchemeOptions>(schemeOptions, loggerFactory, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var configuredApiKeys = authOptions.Value.ApiKeys
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Key))
            .ToList();

        var legacyApiKey = authOptions.Value.ApiKey?.Trim();
        var hasAnyConfiguredKey = configuredApiKeys.Count > 0 || !string.IsNullOrWhiteSpace(legacyApiKey);

        if (!hasAnyConfiguredKey)
        {
            if (environment.IsDevelopment() && authOptions.Value.AllowAnonymousInDevelopment)
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            return Task.FromResult(AuthenticateResult.Fail("API key authentication is not configured."));
        }

        if (!Request.Headers.TryGetValue(authOptions.Value.ApiKeyHeaderName, out var suppliedKeys))
        {
            return Task.FromResult(AuthenticateResult.Fail($"Missing {authOptions.Value.ApiKeyHeaderName} header."));
        }

        var suppliedKey = suppliedKeys.FirstOrDefault()?.Trim();
        if (string.IsNullOrWhiteSpace(suppliedKey))
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid API key."));
        }

        IReadOnlyList<string>? scopes = null;
        var namedApiKey = configuredApiKeys.FirstOrDefault(entry => string.Equals(entry.Key.Trim(), suppliedKey, StringComparison.Ordinal));
        if (namedApiKey is not null)
        {
            scopes = namedApiKey.Scopes;
        }
        else if (!string.IsNullOrWhiteSpace(legacyApiKey) && string.Equals(suppliedKey, legacyApiKey, StringComparison.Ordinal))
        {
            scopes =
            [
                AuthorizationPolicies.ChatUser,
                AuthorizationPolicies.Operator,
                AuthorizationPolicies.KnowledgeAdmin
            ];
        }

        if (scopes is null)
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid API key."));
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, namedApiKey?.Name ?? "local-api-key")
        };

        foreach (var scope in scopes.Where(scope => !string.IsNullOrWhiteSpace(scope)).Distinct(StringComparer.Ordinal))
        {
            claims.Add(new Claim("scope", scope));
        }

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}

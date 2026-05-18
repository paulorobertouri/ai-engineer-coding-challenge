using Api.Services;
using Xunit;

namespace Api.Tests.Services;

public sealed class SensitiveDataRedactorTests
{
    [Fact]
    public void Sanitize_RedactsEmailApiKeyBearerAndCardLikeData()
    {
        var input = "Contact jane.doe@example.com with key sk-abcdefghijklmnopqrstuvwxyz1234 and Bearer abc.def.ghi 4111 1111 1111 1111";

        var output = SensitiveDataRedactor.Sanitize(input);

        Assert.NotNull(output);
        Assert.Contains("[redacted:email]", output);
        Assert.Contains("[redacted:api-key]", output);
        Assert.Contains("Bearer [redacted:token]", output);
        Assert.Contains("[redacted:card]", output);
        Assert.DoesNotContain("example.com", output, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("sk-abcdefghijklmnopqrstuvwxyz1234", output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Sanitize_LeavesSafeTextUnchanged()
    {
        var input = "Ingest failed because source file was missing required section heading.";

        var output = SensitiveDataRedactor.Sanitize(input);

        Assert.Equal(input, output);
    }
}

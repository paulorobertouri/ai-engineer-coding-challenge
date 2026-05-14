using Api.Options;
using Xunit;

namespace Api.Tests.Options;

public class VectorStoreOptionsValidatorTests
{
    private readonly VectorStoreOptionsValidator _validator = new();

    [Theory]
    [InlineData("json")]
    [InlineData("JSON")]
    [InlineData(" json ")]
    public void Validate_ReturnsSuccess_ForSupportedProviders(string provider)
    {
        var options = new VectorStoreOptions { Provider = provider };

        var result = _validator.Validate(name: null, options);

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("redis")]
    [InlineData("postgres")]
    public void Validate_ReturnsFailure_ForUnsupportedOrEmptyProvider(string provider)
    {
        var options = new VectorStoreOptions { Provider = provider };

        var result = _validator.Validate(name: null, options);

        Assert.False(result.Succeeded);
    }
}

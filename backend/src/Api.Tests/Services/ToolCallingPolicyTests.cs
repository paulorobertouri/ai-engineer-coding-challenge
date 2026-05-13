using Api.Services;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Api.Tests;

public class ToolCallingPolicyTests
{
    [Fact]
    public void IsEnabled_WhenUnset_DefaultsToTrue()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection().Build();

        var enabled = ToolCallingPolicy.IsEnabled(config);

        Assert.True(enabled);
    }

    [Fact]
    public void IsEnabled_WhenConfiguredFalse_ReturnsFalse()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OpenAI:EnableTools"] = "false"
            })
            .Build();

        var enabled = ToolCallingPolicy.IsEnabled(config);

        Assert.False(enabled);
    }

    [Fact]
    public void TryExtractSearchQuery_WithInvalidJson_ReturnsInvalidJson()
    {
        var result = ToolCallingPolicy.TryExtractSearchQuery("{query:", out var query);

        Assert.Equal(ToolCallQueryParseResult.InvalidJson, result);
        Assert.Equal(string.Empty, query);
    }

    [Fact]
    public void TryExtractSearchQuery_WithEmptyQuery_ReturnsEmptyQuery()
    {
        var result = ToolCallingPolicy.TryExtractSearchQuery("{\"query\":\"   \"}", out var query);

        Assert.Equal(ToolCallQueryParseResult.EmptyQuery, result);
        Assert.True(string.IsNullOrWhiteSpace(query));
    }

    [Fact]
    public void BuildToolContext_WithNoMatches_ReturnsEmptyString()
    {
        var context = ToolCallingPolicy.BuildToolContext([]);

        Assert.Equal(string.Empty, context);
    }
}

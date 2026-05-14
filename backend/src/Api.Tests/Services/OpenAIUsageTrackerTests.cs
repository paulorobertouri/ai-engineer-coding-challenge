using Api.Options;
using Api.Services;
using Microsoft.Extensions.Options;
using Xunit;

namespace Api.Tests;

public class OpenAIUsageTrackerTests
{
    [Fact]
    public void BuildEstimated_ForFallback_SetsZeroExternalCost()
    {
        var tracker = new OpenAIUsageTracker(Microsoft.Extensions.Options.Options.Create(new OpenAIOptions
        {
            Pricing =
            [
                new OpenAIModelPricingOptions
                {
                    Model = "gpt-4o-mini",
                    PromptTokensPerMillionUsd = 0.15m,
                    CompletionTokensPerMillionUsd = 0.6m,
                    EmbeddingTokensPerMillionUsd = 0m
                }
            ]
        }));

        var usage = tracker.BuildEstimated("fallback", "hello world", "answer", "query", source: "fallback", isExternalCost: false);

        Assert.True(usage.IsEstimated);
        Assert.Equal("fallback", usage.Source);
        Assert.Equal(0m, usage.EstimatedCostUsd);
        Assert.Equal("fallback", usage.Model);
        Assert.True(usage.TotalTokens > 0);
    }

    [Fact]
    public void BuildFromSdkOrEstimate_WhenUsagePresent_UsesReflectionData()
    {
        var tracker = new OpenAIUsageTracker(Microsoft.Extensions.Options.Options.Create(new OpenAIOptions
        {
            Pricing =
            [
                new OpenAIModelPricingOptions
                {
                    Model = "gpt-4o-mini",
                    PromptTokensPerMillionUsd = 0.15m,
                    CompletionTokensPerMillionUsd = 0.6m,
                    EmbeddingTokensPerMillionUsd = 0.02m
                }
            ]
        }));

        var fakeChatCompletion = new
        {
            Usage = new
            {
                PromptTokens = 12,
                CompletionTokens = 8,
                TotalTokens = 20
            }
        };

        var usage = tracker.BuildFromSdkOrEstimate(fakeChatCompletion, "gpt-4o-mini", "prompt text", "completion text", "embedding text");

        Assert.Equal("sdk", usage.Source);
        Assert.Equal(12, usage.PromptTokens);
        Assert.Equal(8, usage.CompletionTokens);
        Assert.Equal(20, usage.TotalTokens);
        Assert.Equal(0.00000668m, usage.EstimatedCostUsd);
    }
}

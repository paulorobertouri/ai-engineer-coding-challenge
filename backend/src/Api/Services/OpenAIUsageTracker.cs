using Api.Contracts;
using Api.Options;
using Microsoft.Extensions.Options;
using System.Reflection;

namespace Api.Services;

public sealed class OpenAIUsageTracker(IOptions<OpenAIOptions> options)
{
    private readonly IReadOnlyDictionary<string, OpenAIModelPricingOptions> _pricing = options.Value.Pricing
        .Where(entry => !string.IsNullOrWhiteSpace(entry.Model))
        .GroupBy(entry => entry.Model.Trim(), StringComparer.OrdinalIgnoreCase)
        .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

    public ChatUsageDto BuildEstimated(string model, string promptText, string completionText, string embeddingText = "", string source = "estimated", bool isExternalCost = true)
    {
        var promptTokens = EstimateTokens(promptText);
        var completionTokens = EstimateTokens(completionText);
        var embeddingTokens = EstimateTokens(embeddingText);
        var estimatedCost = isExternalCost
            ? CalculateCost(model, promptTokens, completionTokens, embeddingTokens)
            : 0m;

        return new ChatUsageDto
        {
            Model = model,
            Source = source,
            IsEstimated = true,
            PromptTokens = promptTokens,
            CompletionTokens = completionTokens,
            EmbeddingTokens = embeddingTokens,
            TotalTokens = promptTokens + completionTokens + embeddingTokens,
            EstimatedCostUsd = estimatedCost
        };
    }

    public ChatUsageDto BuildFromSdkOrEstimate(object chatCompletion, string model, string promptText, string completionText, string embeddingText = "")
    {
        var usage = TryReadUsage(chatCompletion);
        if (usage is not null)
        {
            var snapshot = usage.Value;
            var promptTokens = snapshot.PromptTokens >= 0 ? snapshot.PromptTokens : EstimateTokens(promptText);
            var completionTokens = snapshot.CompletionTokens >= 0 ? snapshot.CompletionTokens : EstimateTokens(completionText);
            var embeddingTokens = EstimateTokens(embeddingText);

            return new ChatUsageDto
            {
                Model = model,
                Source = "sdk",
                IsEstimated = snapshot.IsEstimated,
                PromptTokens = promptTokens,
                CompletionTokens = completionTokens,
                EmbeddingTokens = embeddingTokens,
                TotalTokens = snapshot.TotalTokens >= 0 ? snapshot.TotalTokens : promptTokens + completionTokens + embeddingTokens,
                EstimatedCostUsd = CalculateCost(model, promptTokens, completionTokens, embeddingTokens)
            };
        }

        return BuildEstimated(model, promptText, completionText, embeddingText, source: "estimated");
    }

    private static int EstimateTokens(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return 0;
        }

        return Math.Max(1, (int)Math.Ceiling(text.Length / 4.0));
    }

    private decimal CalculateCost(string model, int promptTokens, int completionTokens, int embeddingTokens)
    {
        if (!_pricing.TryGetValue(model, out var pricing))
        {
            return 0m;
        }

        return (promptTokens * pricing.PromptTokensPerMillionUsd / 1_000_000m)
            + (completionTokens * pricing.CompletionTokensPerMillionUsd / 1_000_000m)
            + (embeddingTokens * pricing.EmbeddingTokensPerMillionUsd / 1_000_000m);
    }

    private static UsageSnapshot? TryReadUsage(object chatCompletion)
    {
        var usageProperty = chatCompletion.GetType().GetProperty("Usage", BindingFlags.Public | BindingFlags.Instance);
        if (usageProperty?.GetValue(chatCompletion) is null)
        {
            return null;
        }

        var usage = usageProperty.GetValue(chatCompletion);
        if (usage is null)
        {
            return null;
        }

        var promptTokens = ReadIntProperty(usage, "PromptTokens", "InputTokens", "PromptTokenCount", "InputTokenCount");
        var completionTokens = ReadIntProperty(usage, "CompletionTokens", "OutputTokens", "CompletionTokenCount", "OutputTokenCount");
        var totalTokens = ReadIntProperty(usage, "TotalTokens", "TokenCount", "UsageTokens");

        return new UsageSnapshot(
            promptTokens,
            completionTokens,
            totalTokens,
            false);
    }

    private static int ReadIntProperty(object instance, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            var property = instance.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
            if (property is null)
            {
                continue;
            }

            var value = property.GetValue(instance);
            if (value is null)
            {
                continue;
            }

            if (value is int intValue)
            {
                return intValue;
            }

            if (int.TryParse(value.ToString(), out var parsed))
            {
                return parsed;
            }
        }

        return -1;
    }

    private readonly record struct UsageSnapshot(int PromptTokens, int CompletionTokens, int TotalTokens, bool IsEstimated);
}

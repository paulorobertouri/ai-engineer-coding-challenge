using Api.Options;
using Microsoft.Extensions.Options;

namespace Api.Services;

public sealed class RuleBasedUserQueryGuardrailService(IOptions<GuardrailOptions> options) : IUserQueryGuardrailService
{
    private readonly GuardrailOptions _options = options.Value;

    public GuardrailDecision Evaluate(string userMessage)
    {
        if (!_options.EnableRuleBasedGuardrails || string.IsNullOrWhiteSpace(userMessage))
        {
            return GuardrailDecision.None;
        }

        var normalizedMessage = userMessage.Trim();
        foreach (var category in _options.Categories)
        {
            if (string.IsNullOrWhiteSpace(category.Name) || category.Keywords.Count == 0)
            {
                continue;
            }

            if (category.Keywords.Any(keyword =>
                    !string.IsNullOrWhiteSpace(keyword) &&
                    normalizedMessage.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
            {
                return new GuardrailDecision
                {
                    IsEscalated = true,
                    Category = category.Name.Trim().ToLowerInvariant(),
                    EscalationMessage = string.IsNullOrWhiteSpace(category.EscalationMessage)
                        ? "I cannot provide guidance for this topic. Please contact your manager or official support channel."
                        : category.EscalationMessage
                };
            }
        }

        return GuardrailDecision.None;
    }
}

using Api.Options;
using Api.Services;
using Microsoft.Extensions.Options;
using Xunit;

namespace Api.Tests;

public class RuleBasedUserQueryGuardrailServiceTests
{
    [Fact]
    public void Evaluate_WhenDisabled_ReturnsNone()
    {
        var service = new RuleBasedUserQueryGuardrailService(Microsoft.Extensions.Options.Options.Create(new GuardrailOptions
        {
            EnableRuleBasedGuardrails = false,
            Categories =
            [
                new GuardrailCategoryOptions
                {
                    Name = "medical",
                    EscalationMessage = "medical escalation",
                    Keywords = ["injury"]
                }
            ]
        }));

        var decision = service.Evaluate("There was an injury on shift");

        Assert.False(decision.IsEscalated);
    }

    [Fact]
    public void Evaluate_WhenKeywordMatches_ReturnsEscalationDecision()
    {
        var service = new RuleBasedUserQueryGuardrailService(Microsoft.Extensions.Options.Options.Create(new GuardrailOptions
        {
            EnableRuleBasedGuardrails = true,
            Categories =
            [
                new GuardrailCategoryOptions
                {
                    Name = "legal",
                    EscalationMessage = "Please contact legal.",
                    Keywords = ["lawsuit", "attorney"]
                }
            ]
        }));

        var decision = service.Evaluate("A customer threatened a lawsuit.");

        Assert.True(decision.IsEscalated);
        Assert.Equal("legal", decision.Category);
        Assert.Equal("Please contact legal.", decision.EscalationMessage);
    }

    [Fact]
    public void Evaluate_WhenCategoryHasNoMessage_UsesDefaultEscalationMessage()
    {
        var service = new RuleBasedUserQueryGuardrailService(Microsoft.Extensions.Options.Options.Create(new GuardrailOptions
        {
            EnableRuleBasedGuardrails = true,
            Categories =
            [
                new GuardrailCategoryOptions
                {
                    Name = "hr",
                    Keywords = ["harassment"]
                }
            ]
        }));

        var decision = service.Evaluate("I need advice for a harassment complaint.");

        Assert.True(decision.IsEscalated);
        Assert.Equal("hr", decision.Category);
        Assert.Contains("manager", decision.EscalationMessage, StringComparison.OrdinalIgnoreCase);
    }
}

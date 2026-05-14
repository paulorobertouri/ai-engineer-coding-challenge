namespace Api.Options;

public sealed class GuardrailOptions
{
    public const string SectionName = "Guardrails";

    public bool EnableRuleBasedGuardrails { get; init; } = true;

    public List<GuardrailCategoryOptions> Categories { get; init; } = [];
}

public sealed class GuardrailCategoryOptions
{
    public string Name { get; init; } = string.Empty;

    public string EscalationMessage { get; init; } = string.Empty;

    public List<string> Keywords { get; init; } = [];
}

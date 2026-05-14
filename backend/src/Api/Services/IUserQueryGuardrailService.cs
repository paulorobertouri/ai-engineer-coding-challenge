namespace Api.Services;

public interface IUserQueryGuardrailService
{
    GuardrailDecision Evaluate(string userMessage);
}

public sealed class GuardrailDecision
{
    public static readonly GuardrailDecision None = new();

    public bool IsEscalated { get; init; }

    public string Category { get; init; } = string.Empty;

    public string EscalationMessage { get; init; } = string.Empty;
}

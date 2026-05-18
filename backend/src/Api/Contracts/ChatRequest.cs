using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace Api.Contracts;

public sealed class ChatRequest : IValidatableObject
{
    public const int MaxMessages = 20;
    public const int MaxConversationIdLength = 128;
    public const int MaxMessageContentLength = 4000;
    public const int MaxKnowledgeBaseIdLength = 64;
    public const int MaxUserRoleLength = 32;
    public const int MaxResponseLanguageLength = 16;
    private static readonly Regex ResponseLanguageRegex = new(
        "^[a-z]{2}(?:-[A-Z]{2})?$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    [Required]
    [MaxLength(MaxConversationIdLength, ErrorMessage = "ConversationId must not exceed 128 characters.")]
    public string ConversationId { get; init; } = Guid.NewGuid().ToString("N");

    [Required]
    [MinLength(1, ErrorMessage = "At least one chat message is required.")]
    [MaxLength(MaxMessages, ErrorMessage = "Chat requests are limited to 20 messages.")]
    public List<ChatMessageDto> Messages { get; init; } = [];

    [MaxLength(MaxKnowledgeBaseIdLength, ErrorMessage = "KnowledgeBaseId must not exceed 64 characters.")]
    [RegularExpression("^[a-zA-Z0-9._-]*$", ErrorMessage = "KnowledgeBaseId may only contain letters, digits, '.', '_', or '-'.")]
    public string? KnowledgeBaseId { get; init; }

    [MaxLength(MaxUserRoleLength, ErrorMessage = "UserRole must not exceed 32 characters.")]
    [RegularExpression("^(cashier|manager|department_lead)?$", ErrorMessage = "UserRole must be cashier, manager, or department_lead.")]
    public string? UserRole { get; init; }

    [MaxLength(MaxResponseLanguageLength, ErrorMessage = "ResponseLanguage must not exceed 16 characters.")]
    [RegularExpression("^[a-z]{2}(?:-[A-Z]{2})?$", ErrorMessage = "ResponseLanguage must be a language tag like 'en', 'es', or 'pt-BR'.")]
    public string? ResponseLanguage { get; init; }

    [RegularExpression("^(neutral|formal|friendly)?$", ErrorMessage = "ResponseTone must be neutral, formal, or friendly.")]
    public string? ResponseTone { get; init; }

    [RegularExpression("^(short|medium|long)?$", ErrorMessage = "ResponseLength must be short, medium, or long.")]
    public string? ResponseLength { get; init; }

    [RegularExpression("^(paragraph|bullets|checklist)?$", ErrorMessage = "ResponseFormat must be paragraph, bullets, or checklist.")]
    public string? ResponseFormat { get; init; }

    // Preserved for backward compatibility; server configuration controls tool usage.
    public bool UseTools { get; init; } = true;

    public static bool IsValidResponseLanguage(string? responseLanguage)
    {
        if (string.IsNullOrWhiteSpace(responseLanguage))
        {
            return true;
        }

        return ResponseLanguageRegex.IsMatch(responseLanguage);
    }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!Messages.Any(m => string.Equals(m.Role, "user", StringComparison.OrdinalIgnoreCase)))
        {
            yield return new ValidationResult(
                "At least one user message is required.",
                [nameof(Messages)]);
        }

        if (!IsValidResponseLanguage(ResponseLanguage))
        {
            yield return new ValidationResult(
                "ResponseLanguage must be a language tag like 'en', 'es', or 'pt-BR'.",
                [nameof(ResponseLanguage)]);
        }
    }
}
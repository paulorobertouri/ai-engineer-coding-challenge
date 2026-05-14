using System.ComponentModel.DataAnnotations;

namespace Api.Contracts;

public sealed class ChatRequest : IValidatableObject
{
    public const int MaxMessages = 20;
    public const int MaxConversationIdLength = 128;
    public const int MaxMessageContentLength = 4000;
    public const int MaxKnowledgeBaseIdLength = 64;

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

    // Preserved for backward compatibility; server configuration controls tool usage.
    public bool UseTools { get; init; } = true;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!Messages.Any(m => string.Equals(m.Role, "user", StringComparison.OrdinalIgnoreCase)))
        {
            yield return new ValidationResult(
                "At least one user message is required.",
                [nameof(Messages)]);
        }
    }
}
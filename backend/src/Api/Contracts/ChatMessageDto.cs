using System.ComponentModel.DataAnnotations;

namespace Api.Contracts;

public sealed class ChatMessageDto
{
    [Required]
    [RegularExpression("^(user|assistant|system)$", ErrorMessage = "Role must be user, assistant, or system.")]
    public string Role { get; init; } = "user";

    [Required]
    [MinLength(1, ErrorMessage = "Message content cannot be empty.")]
    [MaxLength(ChatRequest.MaxMessageContentLength, ErrorMessage = "Message content must not exceed 4000 characters.")]
    public string Content { get; init; } = string.Empty;

    public DateTimeOffset TimestampUtc { get; init; } = DateTimeOffset.UtcNow;
}
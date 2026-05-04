using System.ComponentModel.DataAnnotations;

namespace Api.Contracts;

public sealed class ChatMessageDto
{
    [Required]
    [RegularExpression("^(user|assistant|system)$", ErrorMessage = "Role must be user, assistant, or system.")]
    public string Role { get; init; } = "user";

    [Required]
    public string Content { get; init; } = string.Empty;

    public DateTimeOffset TimestampUtc { get; init; } = DateTimeOffset.UtcNow;
}
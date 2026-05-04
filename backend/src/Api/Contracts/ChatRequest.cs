using System.ComponentModel.DataAnnotations;

namespace Api.Contracts;

public sealed class ChatRequest
{
    [Required]
    public string ConversationId { get; init; } = Guid.NewGuid().ToString("N");

    [Required]
    [MinLength(1, ErrorMessage = "At least one chat message is required.")]
    public List<ChatMessageDto> Messages { get; init; } = [];

    public bool UseTools { get; init; } = true;
}
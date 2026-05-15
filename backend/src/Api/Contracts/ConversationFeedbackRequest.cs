using System.ComponentModel.DataAnnotations;

namespace Api.Contracts;

public sealed class ConversationFeedbackRequest
{
    public const int MaxConversationIdLength = 128;
    public const int MaxMessageIdLength = 128;
    public const int MaxCommentLength = 500;

    [Required]
    [MaxLength(MaxConversationIdLength, ErrorMessage = "ConversationId must not exceed 128 characters.")]
    public string ConversationId { get; init; } = string.Empty;

    [Required]
    [MaxLength(MaxMessageIdLength, ErrorMessage = "MessageId must not exceed 128 characters.")]
    public string MessageId { get; init; } = string.Empty;

    [Required]
    [RegularExpression("^(helpful|unhelpful|wrong-citation)$", ErrorMessage = "FeedbackType must be one of: helpful, unhelpful, wrong-citation.")]
    public string FeedbackType { get; init; } = string.Empty;

    [MaxLength(MaxCommentLength, ErrorMessage = "Comment must not exceed 500 characters.")]
    public string? Comment { get; init; }
}

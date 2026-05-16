using Api.Contracts;

namespace Api.Application.Chat;

public sealed record SendChatMessageCommand(ChatRequest Request);

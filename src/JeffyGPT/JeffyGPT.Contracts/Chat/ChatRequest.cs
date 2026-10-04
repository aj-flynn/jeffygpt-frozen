namespace JeffyGPT.Contracts.Chat;

public sealed record ChatRequest(IReadOnlyList<ChatMessageDto> Messages);

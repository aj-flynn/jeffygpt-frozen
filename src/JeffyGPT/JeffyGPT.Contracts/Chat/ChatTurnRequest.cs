namespace JeffyGPT.Contracts.Chat;

/// <summary>
/// Body of POST /api/chat and POST /api/chat/stream.
/// Omit <see cref="ConversationId"/> to start a conversation.
/// </summary>
public sealed record ChatTurnRequest(string? ConversationId, string? Content);

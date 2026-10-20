namespace JeffyGPT.Contracts.Chat;

public sealed record ChatResponse(
    string ConversationId,
    ChatMessageDto AssistantMessage,
    IReadOnlyList<ToolCallDto> ToolCalls,
    IReadOnlyList<ToolResultDto> ToolResults);

namespace JeffyGPT.Contracts.Chat;

public sealed record ChatResponse(
    ChatMessageDto AssistantMessage,
    IReadOnlyList<ToolCallDto> ToolCalls,
    IReadOnlyList<ToolResultDto> ToolResults);

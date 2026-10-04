namespace JeffyGPT.Contracts.Chat;

public sealed record AssistantCompletion(
    ChatMessageDto AssistantMessage,
    IReadOnlyList<ToolCallDto> ToolCalls);

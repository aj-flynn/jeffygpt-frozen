namespace JeffyGPT.Contracts.Chat;

public sealed record ChatMessageDto(
    string Role,
    string Content,
    IReadOnlyList<ToolCallDto>? ToolCalls = null,
    string? ToolCallId = null);

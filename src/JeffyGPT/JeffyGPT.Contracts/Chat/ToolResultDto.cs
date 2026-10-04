namespace JeffyGPT.Contracts.Chat;

public sealed record ToolResultDto(string ToolCallId, string ContentJson, bool IsError);

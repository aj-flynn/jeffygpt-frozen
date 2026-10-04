namespace JeffyGPT.Contracts.Chat;

public sealed record ToolCallDto(string Id, string Name, string ArgumentsJson);

namespace JeffyGPT.Application.Chat;

public sealed record ToolArgumentValidationResult(bool IsValid, string? ErrorMessage);

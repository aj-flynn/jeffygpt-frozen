namespace JeffyGPT.Contracts.Chat;

public sealed record ToolDefinitionDto(string Name, string Description, string ParametersJsonSchema);

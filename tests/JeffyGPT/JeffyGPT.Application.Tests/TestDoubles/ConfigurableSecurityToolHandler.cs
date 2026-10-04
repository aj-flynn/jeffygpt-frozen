using System.Text.Json;
using JeffyGPT.Application.Chat;
using JeffyGPT.Application.Chat.Tools;
using JeffyGPT.Contracts.Chat;

namespace JeffyGPT.Application.Tests.TestDoubles;

public sealed class ConfigurableSecurityToolHandler : IToolHandler
{
    public const string ToolName = "configurable-security-tool";

    private static readonly ToolDefinitionDto ToolDefinition = new(
        ToolName,
        "Test tool with configurable security metadata.",
        """{"type":"object","properties":{},"additionalProperties":false}""");

    public ConfigurableSecurityToolHandler(ToolSecurityDescriptor security)
    {
        Security = security;
    }

    public string Name => ToolName;

    public ToolDefinitionDto Definition => ToolDefinition;

    public ToolSecurityDescriptor Security { get; }

    public Task<ToolResultDto> ExecuteAsync(ToolCallDto toolCall, CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;

        return Task.FromResult(new ToolResultDto(
            toolCall.Id,
            JsonSerializer.Serialize(new { ok = true }),
            IsError: false));
    }
}

using System.Text.Json;
using JeffyGPT.Application.Chat;
using JeffyGPT.Application.Chat.Tools;
using JeffyGPT.Contracts.Chat;

namespace JeffyGPT.Application.Tests.TestDoubles;

public sealed class EchoQueryToolHandler : IToolHandler
{
    public const string ToolName = "echo-query";

    private static readonly ToolDefinitionDto ToolDefinition = new(
        ToolName,
        "Echoes a query string.",
        """
        {
          "type": "object",
          "properties": {
            "query": { "type": "string" }
          },
          "required": ["query"],
          "additionalProperties": false
        }
        """);

    public string Name => ToolName;

    public ToolDefinitionDto Definition => ToolDefinition;

    public ToolSecurityDescriptor Security { get; } = new(
        ToolAccessKind.ReadOnly,
        ToolDataExposure.None,
        RequiresConfirmation: false);

    public Task<ToolResultDto> ExecuteAsync(ToolCallDto toolCall, CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;

        using var document = JsonDocument.Parse(toolCall.ArgumentsJson);
        var query = document.RootElement.GetProperty("query").GetString() ?? string.Empty;

        return Task.FromResult(new ToolResultDto(
            toolCall.Id,
            JsonSerializer.Serialize(new { query }),
            IsError: false));
    }
}

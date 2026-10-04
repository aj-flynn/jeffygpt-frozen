using System.Text.Json;
using JeffyGPT.Application.Chat;
using JeffyGPT.Application.Chat.Tools;
using JeffyGPT.Contracts.Chat;

namespace JeffyGPT.Application.Tests.TestDoubles;

public sealed class GetCurrentTimeToolHandler : IToolHandler
{
    private const string ToolName = "get-current-time";

    private static readonly ToolDefinitionDto ToolDefinition = new(
        ToolName,
        "Gets the current local time.",
        """{"type":"object","properties":{},"additionalProperties":false}""");

    public string Name => ToolName;

    public ToolDefinitionDto Definition => ToolDefinition;

    public ToolSecurityDescriptor Security { get; } = new(
        ToolAccessKind.ReadOnly,
        ToolDataExposure.RuntimeMetadata,
        RequiresConfirmation: false,
        SafetyNotes: "Returns current local server time only.");

    public Task<ToolResultDto> ExecuteAsync(ToolCallDto toolCall, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(new ToolResultDto(
            toolCall.Id,
            JsonSerializer.Serialize(new { local = DateTimeOffset.Now.ToString("O") }),
            IsError: false));
    }
}

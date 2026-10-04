using System.Text.Json;
using JeffyGPT.Application.Chat;
using JeffyGPT.Application.Chat.Tools;
using JeffyGPT.Contracts.Chat;
using Json.Schema;

namespace JeffyGPT.Infrastructure.Tools;

public class ToolExecutor : IToolExecutor
{
    private readonly IReadOnlyDictionary<string, RegisteredToolHandler> _handlers;
    private readonly IToolArgumentValidator _argumentValidator;
    private readonly IToolExecutionPolicy _executionPolicy;

    public ToolExecutor(
        IEnumerable<IToolHandler> handlers,
        IToolArgumentValidator argumentValidator,
        IToolExecutionPolicy executionPolicy)
    {
        _argumentValidator = argumentValidator;
        _executionPolicy = executionPolicy;
        _handlers = handlers.ToDictionary(
            handler => handler.Name,
            handler => new RegisteredToolHandler(
                handler,
                JsonSchema.FromText(handler.Definition.ParametersJsonSchema)),
            StringComparer.OrdinalIgnoreCase);
    }

    public async Task<ToolResultDto> ExecuteAsync(ToolCallDto toolCall, CancellationToken cancellationToken = default)
    {
        if (!_handlers.TryGetValue(toolCall.Name, out var registeredHandler))
        {
            return ErrorResult(toolCall.Id, $"Unknown tool: {toolCall.Name}");
        }

        var policyResult = _executionPolicy.Evaluate(registeredHandler.Handler);
        if (!policyResult.IsAllowed)
        {
            return ErrorResult(
                toolCall.Id,
                $"Tool execution is not allowed by policy: {policyResult.ErrorMessage}");
        }

        var validation = _argumentValidator.Validate(
            registeredHandler.Schema,
            toolCall.Name,
            toolCall.ArgumentsJson);

        if (!validation.IsValid)
        {
            return ErrorResult(toolCall.Id, validation.ErrorMessage ?? "Tool arguments are invalid.");
        }

        return await registeredHandler.Handler.ExecuteAsync(toolCall, cancellationToken);
    }

    private static ToolResultDto ErrorResult(string toolCallId, string message) =>
        new(toolCallId, JsonSerializer.Serialize(new { error = message }), IsError: true);

    private sealed record RegisteredToolHandler(IToolHandler Handler, JsonSchema Schema);
}

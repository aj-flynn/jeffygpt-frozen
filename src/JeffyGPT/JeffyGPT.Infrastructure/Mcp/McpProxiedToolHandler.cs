using System.Text.Json;
using JeffyGPT.Application.Chat;
using JeffyGPT.Application.Chat.Tools;
using JeffyGPT.Contracts.Chat;
using Microsoft.Extensions.Logging;

namespace JeffyGPT.Infrastructure.Mcp;

public sealed class McpProxiedToolHandler : IToolHandler
{
    private readonly IMcpConnectionManager _connectionManager;
    private readonly McpDiscoveredTool _discoveredTool;
    private readonly ILogger? _logger;

    private McpProxiedToolHandler(
        IMcpConnectionManager connectionManager,
        McpDiscoveredTool discoveredTool,
        ToolDefinitionDto definition,
        ILogger? logger)
    {
        _connectionManager = connectionManager;
        _discoveredTool = discoveredTool;
        Definition = definition;
        Security = ToolSecurityDescriptor.ProxiedMcpTool();
        _logger = logger;
    }

    public string Name => _discoveredTool.ExposedToolName;

    public ToolDefinitionDto Definition { get; }

    public ToolSecurityDescriptor Security { get; }

    public static McpProxiedToolHandler Create(
        IMcpConnectionManager connectionManager,
        McpDiscoveredTool discoveredTool,
        ILogger? logger = null) =>
        new(
            connectionManager,
            discoveredTool,
            new ToolDefinitionDto(
                discoveredTool.ExposedToolName,
                discoveredTool.Description,
                discoveredTool.ParametersJsonSchema),
            logger);

    public async Task<ToolResultDto> ExecuteAsync(ToolCallDto toolCall, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var arguments = McpJsonHelpers.ParseArguments(toolCall.ArgumentsJson);
            var result = await _connectionManager.CallToolAsync(
                _discoveredTool.ServerId,
                _discoveredTool.OriginalToolName,
                arguments,
                cancellationToken);

            return new ToolResultDto(
                toolCall.Id,
                McpJsonHelpers.SerializeToolResultContent(result),
                result.IsError ?? false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger?.LogError(
                ex,
                "MCP tool '{ToolName}' on server '{ServerId}' failed.",
                _discoveredTool.OriginalToolName,
                _discoveredTool.ServerId);

            return new ToolResultDto(
                toolCall.Id,
                JsonSerializer.Serialize(new { error = "Tool execution failed." }),
                IsError: true);
        }
    }
}

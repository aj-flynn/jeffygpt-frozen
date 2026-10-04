using System.Text.Json;
using JeffyGPT.Infrastructure.Mcp;
using ModelContextProtocol.Protocol;

namespace JeffyGPT.Application.Tests.TestDoubles;

public sealed class FakeMcpConnectionManager : IMcpConnectionManager
{
    private readonly IReadOnlyList<McpDiscoveredTool> _tools;
    private readonly Func<string, string, IReadOnlyDictionary<string, object?>, CallToolResult>? _callTool;

    public FakeMcpConnectionManager(
        IReadOnlyList<McpDiscoveredTool> tools,
        Func<string, string, IReadOnlyDictionary<string, object?>, CallToolResult>? callTool = null)
    {
        _tools = tools;
        _callTool = callTool;
    }

    public IReadOnlyList<McpDiscoveredTool> GetDiscoveredTools() => _tools;

    public Task InitializeAsync(CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task<CallToolResult> CallToolAsync(
        string serverId,
        string toolName,
        IReadOnlyDictionary<string, object?> arguments,
        CancellationToken cancellationToken = default)
    {
        if (_callTool is not null)
        {
            return Task.FromResult(_callTool(serverId, toolName, arguments));
        }

        return Task.FromResult(new CallToolResult
        {
            Content =
            [
                new TextContentBlock
                {
                    Text = JsonSerializer.Serialize(new { echoed = toolName, serverId })
                }
            ]
        });
    }
}

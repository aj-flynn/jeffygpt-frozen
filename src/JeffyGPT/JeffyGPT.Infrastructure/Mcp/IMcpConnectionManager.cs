using JeffyGPT.Contracts.Chat;

namespace JeffyGPT.Infrastructure.Mcp;

public interface IMcpConnectionManager
{
    IReadOnlyList<McpDiscoveredTool> GetDiscoveredTools();

    Task InitializeAsync(CancellationToken cancellationToken = default);

    Task<ModelContextProtocol.Protocol.CallToolResult> CallToolAsync(
        string serverId,
        string toolName,
        IReadOnlyDictionary<string, object?> arguments,
        CancellationToken cancellationToken = default);
}

public sealed record McpDiscoveredTool(
    string ServerId,
    string OriginalToolName,
    string ExposedToolName,
    string Description,
    string ParametersJsonSchema);

using JeffyGPT.Application.Chat;
using JeffyGPT.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JeffyGPT.Infrastructure.Mcp;

public sealed class McpToolBootstrapper : IMcpToolBootstrapper
{
    private readonly IMcpConnectionManager _connectionManager;
    private readonly McpToolHandlerRegistry _registry;
    private readonly McpOptions _options;
    private readonly ILogger<McpToolBootstrapper> _logger;

    public McpToolBootstrapper(
        IMcpConnectionManager connectionManager,
        McpToolHandlerRegistry registry,
        IOptions<McpOptions> options,
        ILogger<McpToolBootstrapper> logger)
    {
        _connectionManager = connectionManager;
        _registry = registry;
        _options = options.Value;
        _logger = logger;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var enabledServers = _options.Servers
            .Where(server => server.IsConnectable())
            .ToList();

        if (enabledServers.Count == 0)
        {
            _logger.LogWarning("No MCP servers are enabled. Tool catalog will be empty.");
            _registry.Register([]);
            return;
        }

        foreach (var server in enabledServers)
        {
            if (!server.Policy.IsValid(out var error))
            {
                throw new InvalidOperationException(
                    $"MCP server '{server.Id}' policy is invalid: {error}");
            }
        }

        await _connectionManager.InitializeAsync(cancellationToken);

        var serverLookup = enabledServers.ToDictionary(
            server => server.Id,
            StringComparer.OrdinalIgnoreCase);

        var handlers = new List<IToolHandler>();
        foreach (var tool in _connectionManager.GetDiscoveredTools())
        {
            if (!serverLookup.TryGetValue(tool.ServerId, out var serverOptions))
            {
                throw new InvalidOperationException(
                    $"Discovered tool references unknown MCP server '{tool.ServerId}'.");
            }

            if (!serverOptions.Policy.AllowsTool(tool.OriginalToolName))
            {
                continue;
            }

            handlers.Add(McpProxiedToolHandler.Create(_connectionManager, tool, _logger));
        }

        _registry.Register(handlers);
        _logger.LogInformation(
            "Registered {ToolCount} executable MCP tools from {ServerCount} server(s).",
            handlers.Count,
            enabledServers.Count);
    }
}

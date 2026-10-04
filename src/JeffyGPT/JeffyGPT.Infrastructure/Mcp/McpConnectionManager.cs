using JeffyGPT.Infrastructure.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Client;

namespace JeffyGPT.Infrastructure.Mcp;

public sealed class McpConnectionManager : IMcpConnectionManager, IAsyncDisposable
{
    private readonly McpOptions _options;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly ILogger<McpConnectionManager> _logger;
    private readonly List<McpServerConnection> _connections = [];
    private readonly List<McpDiscoveredTool> _discoveredTools = [];
    private bool _initialized;

    public McpConnectionManager(
        IOptions<McpOptions> options,
        IHostEnvironment hostEnvironment,
        ILogger<McpConnectionManager> logger)
    {
        _options = options.Value;
        _hostEnvironment = hostEnvironment;
        _logger = logger;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_initialized)
        {
            return;
        }

        var enabledServers = _options.Servers
            .Where(server => server.IsConnectable())
            .ToList();

        foreach (var server in enabledServers)
        {
            try
            {
                await ConnectAndDiscoverAsync(server, enabledServers.Count, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Failed to connect to MCP server '{ServerId}'", server.Id);

                if (server.Required)
                {
                    throw new InvalidOperationException(
                        $"Required MCP server '{server.Id}' failed to connect.",
                        ex);
                }
            }
        }

        McpToolNameResolver.ValidateNoDuplicateExposedNames(_discoveredTools);
        _initialized = true;
    }

    private async Task ConnectAndDiscoverAsync(
        McpServerOptions server,
        int enabledServerCount,
        CancellationToken cancellationToken)
    {
        var connection = await ConnectServerAsync(server, cancellationToken);
        _connections.Add(connection);

        var tools = await connection.Client.ListToolsAsync(cancellationToken: cancellationToken);
        foreach (var tool in tools)
        {
            var originalName = tool.ProtocolTool.Name
                ?? throw new InvalidOperationException($"MCP tool from server '{server.Id}' is missing a name.");

            var exposedName = McpToolNameResolver.ResolveExposedName(
                server.Id,
                originalName,
                enabledServerCount);

            _discoveredTools.Add(new McpDiscoveredTool(
                server.Id,
                originalName,
                exposedName,
                tool.Description ?? string.Empty,
                McpJsonHelpers.SerializeInputSchema(tool.JsonSchema)));
        }

        _logger.LogInformation(
            "Connected to MCP server '{ServerId}' with {ToolCount} tool(s).",
            server.Id,
            tools.Count);
    }

    public IReadOnlyList<McpDiscoveredTool> GetDiscoveredTools() => _discoveredTools;

    public async Task<ModelContextProtocol.Protocol.CallToolResult> CallToolAsync(
        string serverId,
        string toolName,
        IReadOnlyDictionary<string, object?> arguments,
        CancellationToken cancellationToken = default)
    {
        var connection = _connections.FirstOrDefault(
            candidate => string.Equals(candidate.ServerId, serverId, StringComparison.OrdinalIgnoreCase));

        if (connection is null)
        {
            throw new InvalidOperationException($"MCP server '{serverId}' is not connected.");
        }

        return await connection.Client.CallToolAsync(toolName, arguments, cancellationToken: cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var connection in _connections)
        {
            await connection.DisposeAsync();
        }

        _connections.Clear();
    }

    private async Task<McpServerConnection> ConnectServerAsync(
        McpServerOptions server,
        CancellationToken cancellationToken)
    {
        IClientTransport transport = server.Transport switch
        {
            McpTransportKind.Stdio => CreateStdioTransport(server),
            McpTransportKind.Http => CreateHttpTransport(server),
            _ => throw new InvalidOperationException($"Unsupported MCP transport '{server.Transport}'.")
        };

        var client = await McpClient.CreateAsync(transport, cancellationToken: cancellationToken);
        return new McpServerConnection(server.Id, client);
    }

    internal StdioClientTransport CreateStdioTransport(McpServerOptions server)
    {
        if (string.IsNullOrWhiteSpace(server.Command))
        {
            throw new InvalidOperationException(
                $"MCP server '{server.Id}' requires Command for stdio transport.");
        }

        var workingDirectory = ResolvePath(
            string.IsNullOrWhiteSpace(server.WorkingDirectory)
                ? _hostEnvironment.ContentRootPath
                : server.WorkingDirectory);

        var arguments = server.Arguments
            .Select(argument => ResolveArgumentPath(argument, workingDirectory))
            .ToList();

        foreach (var argument in arguments.Where(LooksLikeProjectOrAssemblyPath))
        {
            if (!File.Exists(argument))
            {
                throw new InvalidOperationException(
                    $"MCP server '{server.Id}' entry point was not found: '{argument}'. " +
                    "Build JeffyMCP first (dotnet build src/JeffyMCP/JeffyMCP.csproj), " +
                    "or fix JeffyGPT:Mcp:Servers Arguments / WorkingDirectory.");
            }
        }

        _logger.LogInformation(
            "Starting MCP server '{ServerId}' via stdio: {Command} {Arguments} (cwd={WorkingDirectory})",
            server.Id,
            server.Command,
            string.Join(' ', arguments),
            workingDirectory);

        return new StdioClientTransport(new StdioClientTransportOptions
        {
            Name = server.Id,
            Command = server.Command,
            Arguments = arguments,
            WorkingDirectory = workingDirectory
        });
    }

    private static HttpClientTransport CreateHttpTransport(McpServerOptions server)
    {
        if (string.IsNullOrWhiteSpace(server.Endpoint))
        {
            throw new InvalidOperationException(
                $"MCP server '{server.Id}' requires Endpoint for HTTP transport.");
        }

        return new HttpClientTransport(new HttpClientTransportOptions
        {
            Name = server.Id,
            Endpoint = new Uri(server.Endpoint),
            // JeffyMCP HTTP uses WithHttpTransport(Stateless = true) — pin Streamable HTTP.
            TransportMode = HttpTransportMode.StreamableHttp,
            OwnsSession = false
        });
    }

    private string ResolvePath(string path)
    {
        if (Path.IsPathRooted(path))
        {
            return Path.GetFullPath(path);
        }

        return Path.GetFullPath(Path.Combine(_hostEnvironment.ContentRootPath, path));
    }

    private string ResolveArgumentPath(string argument, string workingDirectory)
    {
        // Resolve relative .csproj / .dll paths so "--project ..\JeffyMCP\JeffyMCP.csproj" works
        // regardless of whether the API was launched from the project folder or bin/.
        if (LooksLikeProjectOrAssemblyPath(argument) && !Path.IsPathRooted(argument))
        {
            var fromContentRoot = Path.GetFullPath(Path.Combine(_hostEnvironment.ContentRootPath, argument));
            if (File.Exists(fromContentRoot))
            {
                return fromContentRoot;
            }

            var fromWorkingDirectory = Path.GetFullPath(Path.Combine(workingDirectory, argument));
            if (File.Exists(fromWorkingDirectory))
            {
                return fromWorkingDirectory;
            }

            return fromContentRoot;
        }

        return argument;
    }

    private static bool LooksLikeProjectOrAssemblyPath(string argument)
    {
        return argument.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)
            || argument.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
            || argument.EndsWith(".exe", StringComparison.OrdinalIgnoreCase);
    }

    private sealed class McpServerConnection(string serverId, McpClient client) : IAsyncDisposable
    {
        public string ServerId { get; } = serverId;

        public McpClient Client { get; } = client;

        public async ValueTask DisposeAsync() => await Client.DisposeAsync();
    }
}

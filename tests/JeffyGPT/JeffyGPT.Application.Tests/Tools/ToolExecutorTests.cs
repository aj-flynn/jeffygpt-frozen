using System.Text.Json;
using JeffyGPT.Application.Chat;
using JeffyGPT.Application.Chat.Tools;
using JeffyGPT.Application.Tests.TestDoubles;
using JeffyGPT.Contracts.Chat;
using JeffyGPT.Infrastructure.Mcp;
using JeffyGPT.Infrastructure.Tools;

namespace JeffyGPT.Application.Tests.Tools;

public class ToolExecutorTests
{
    private static readonly IToolHandler[] Handlers = [new GetCurrentTimeToolHandler()];
    private static readonly JsonSchemaToolArgumentValidator Validator = new();

    private static ToolExecutor CreateExecutor(IEnumerable<IToolHandler> handlers) =>
        new(handlers, Validator, new DefaultToolExecutionPolicy());

    [Fact]
    public async Task ExecuteAsync_KnownTool_DispatchesToHandler()
    {
        var executor = CreateExecutor(Handlers);
        var toolCall = new ToolCallDto("call_time", "get-current-time", "{}");

        var result = await executor.ExecuteAsync(toolCall, CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal(toolCall.Id, result.ToolCallId);

        using var document = JsonDocument.Parse(result.ContentJson);
        Assert.True(document.RootElement.TryGetProperty("local", out var localProperty));
        Assert.True(DateTimeOffset.TryParse(localProperty.GetString(), out _));
    }

    [Fact]
    public async Task ExecuteAsync_UnknownTool_ReturnsErrorResult()
    {
        var executor = CreateExecutor(Handlers);
        var toolCall = new ToolCallDto("call_unknown", "unknown_tool", "{}");

        var result = await executor.ExecuteAsync(toolCall, CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal(toolCall.Id, result.ToolCallId);
        Assert.Contains("unknown_tool", result.ContentJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_InvalidArguments_ReturnsErrorResult()
    {
        var executor = CreateExecutor(Handlers);
        var toolCall = new ToolCallDto("call_time", "get-current-time", """{"unexpected":true}""");

        var result = await executor.ExecuteAsync(toolCall, CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal(toolCall.Id, result.ToolCallId);
        Assert.Contains("get-current-time", result.ContentJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_MalformedArgumentsJson_ReturnsErrorResult()
    {
        var executor = CreateExecutor(Handlers);
        var toolCall = new ToolCallDto("call_time", "get-current-time", "{bad json");

        var result = await executor.ExecuteAsync(toolCall, CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal(toolCall.Id, result.ToolCallId);
        Assert.Contains("valid JSON", result.ContentJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_RequiredArgumentsMissing_ReturnsErrorResult()
    {
        IToolHandler[] handlers = [new EchoQueryToolHandler()];
        var executor = CreateExecutor(handlers);
        var toolCall = new ToolCallDto("call_echo", EchoQueryToolHandler.ToolName, "{}");

        var result = await executor.ExecuteAsync(toolCall, CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Contains(EchoQueryToolHandler.ToolName, result.ContentJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_RequiredArgumentsPresent_DispatchesToHandler()
    {
        IToolHandler[] handlers = [new EchoQueryToolHandler()];
        var executor = CreateExecutor(handlers);
        var toolCall = new ToolCallDto(
            "call_echo",
            EchoQueryToolHandler.ToolName,
            """{"query":"hello"}""");

        var result = await executor.ExecuteAsync(toolCall, CancellationToken.None);

        Assert.False(result.IsError);
        using var document = JsonDocument.Parse(result.ContentJson);
        Assert.Equal("hello", document.RootElement.GetProperty("query").GetString());
    }

    [Fact]
    public async Task ExecuteAsync_MutatingTool_BlockedByPolicy()
    {
        IToolHandler[] handlers =
        [
            new ConfigurableSecurityToolHandler(new ToolSecurityDescriptor(
                ToolAccessKind.Mutating,
                ToolDataExposure.None,
                RequiresConfirmation: false))
        ];
        var executor = CreateExecutor(handlers);
        var toolCall = new ToolCallDto("call_mutating", ConfigurableSecurityToolHandler.ToolName, "{}");

        var result = await executor.ExecuteAsync(toolCall, CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Contains("not allowed by policy", result.ContentJson, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("mutating tools are not permitted", result.ContentJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_RequiresConfirmationTool_BlockedByPolicy()
    {
        IToolHandler[] handlers =
        [
            new ConfigurableSecurityToolHandler(new ToolSecurityDescriptor(
                ToolAccessKind.ReadOnly,
                ToolDataExposure.None,
                RequiresConfirmation: true))
        ];
        var executor = CreateExecutor(handlers);
        var toolCall = new ToolCallDto("call_confirm", ConfigurableSecurityToolHandler.ToolName, "{}");

        var result = await executor.ExecuteAsync(toolCall, CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Contains("tool requires user confirmation", result.ContentJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_McpProxiedTool_DispatchesToConnectionManager()
    {
        var handler = CreateMcpHandler();
        var executor = CreateExecutor([handler]);
        var toolCall = new ToolCallDto("call_read", "read-file", """{"rootAlias":"docs","relativePath":"readme.md"}""");

        var result = await executor.ExecuteAsync(toolCall, CancellationToken.None);

        Assert.False(result.IsError);
        using var document = JsonDocument.Parse(result.ContentJson);
        Assert.Equal("read-file", document.RootElement.GetProperty("echoed").GetString());
    }

    private static McpProxiedToolHandler CreateMcpHandler()
    {
        var connectionManager = new FakeMcpConnectionManager(
        [
            new McpDiscoveredTool(
                "jeffymcp",
                "read-file",
                "read-file",
                "Reads a file",
                """{"type":"object","required":["rootAlias","relativePath"],"properties":{"rootAlias":{"type":"string"},"relativePath":{"type":"string"}},"additionalProperties":false}""")
        ]);

        return McpProxiedToolHandler.Create(
            connectionManager,
            connectionManager.GetDiscoveredTools()[0]);
    }
}

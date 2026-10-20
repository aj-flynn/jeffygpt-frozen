using JeffyGPT.Application.Tests.TestDoubles;
using JeffyGPT.Infrastructure.Mcp;

namespace JeffyGPT.Application.Tests.Tools;

public class McpToolCatalogTests
{
    [Fact]
    public void GetTools_BeforeInitialization_ThrowsClearError()
    {
        var catalog = new McpToolCatalog(new McpToolHandlerRegistry());

        var exception = Assert.Throws<InvalidOperationException>(() => catalog.GetTools());

        Assert.Contains("Initialize MCP tools", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Register_AfterInitialization_RejectsReplacement()
    {
        var registry = new McpToolHandlerRegistry();
        registry.Register([new GetCurrentTimeToolHandler()]);

        Assert.Throws<InvalidOperationException>(() => registry.Register([]));
        Assert.Single(registry.Handlers);
    }

    [Fact]
    public void GetTools_InitializedWithoutTools_ReturnsEmptyCatalog()
    {
        var registry = new McpToolHandlerRegistry();
        registry.Register([]);

        Assert.Empty(new McpToolCatalog(registry).GetTools());
    }

    [Fact]
    public void GetTools_ReturnsRegisteredHandlers()
    {
        var registry = new McpToolHandlerRegistry();
        registry.Register([new GetCurrentTimeToolHandler()]);
        var catalog = new McpToolCatalog(registry);

        var tools = catalog.GetTools();

        Assert.Single(tools);
        Assert.Equal("get-current-time", tools[0].Name);
        Assert.Equal("Gets the current local time.", tools[0].Description);
    }
}

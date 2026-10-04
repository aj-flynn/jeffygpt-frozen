using JeffyGPT.Application.Tests.TestDoubles;
using JeffyGPT.Infrastructure.Mcp;

namespace JeffyGPT.Application.Tests.Tools;

public class McpToolCatalogTests
{
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

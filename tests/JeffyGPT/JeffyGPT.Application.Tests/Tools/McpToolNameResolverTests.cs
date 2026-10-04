using JeffyGPT.Application.Chat.Tools;
using JeffyGPT.Infrastructure.Configuration;
using JeffyGPT.Infrastructure.Mcp;

namespace JeffyGPT.Application.Tests.Tools;

public class McpToolNameResolverTests
{
    [Fact]
    public void ResolveExposedName_SingleServer_ReturnsUnprefixedName()
    {
        var exposed = McpToolNameResolver.ResolveExposedName("jeffymcp", "read-file", enabledServerCount: 1);

        Assert.Equal("read-file", exposed);
    }

    [Fact]
    public void ResolveExposedName_MultipleServers_PrefixesWithServerId()
    {
        var exposed = McpToolNameResolver.ResolveExposedName("jeffymcp", "read-file", enabledServerCount: 2);

        Assert.Equal("jeffymcp__read-file", exposed);
    }

    [Fact]
    public void ValidateNoDuplicateExposedNames_ThrowsWhenDuplicatesExist()
    {
        var tools = new[]
        {
            new McpDiscoveredTool("a", "tool", "same-name", "d1", "{}"),
            new McpDiscoveredTool("b", "tool", "same-name", "d2", "{}")
        };

        Assert.Throws<InvalidOperationException>(() =>
            McpToolNameResolver.ValidateNoDuplicateExposedNames(tools));
    }
}

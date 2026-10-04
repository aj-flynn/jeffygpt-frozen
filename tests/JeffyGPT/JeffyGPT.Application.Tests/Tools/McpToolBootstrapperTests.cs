using JeffyGPT.Application.Tests.TestDoubles;
using JeffyGPT.Infrastructure.Configuration;
using JeffyGPT.Infrastructure.Mcp;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace JeffyGPT.Application.Tests.Tools;

public class McpToolBootstrapperTests
{
    [Fact]
    public async Task InitializeAsync_AllowAllTools_RegistersEveryDiscoveredTool()
    {
        var registry = new McpToolHandlerRegistry();
        var bootstrapper = CreateBootstrapper(
            registry,
            new McpServerPolicyOptions { AllowAllTools = true },
            Discover("search"),
            Discover("read-file"));

        await bootstrapper.InitializeAsync();

        Assert.Equal(
            ["search", "read-file"],
            registry.Handlers.Select(handler => handler.Name).ToArray());
    }

    [Fact]
    public async Task InitializeAsync_ApprovedList_RegistersOnlyListedTools()
    {
        var registry = new McpToolHandlerRegistry();
        var bootstrapper = CreateBootstrapper(
            registry,
            new McpServerPolicyOptions { AllowedTools = ["search"] },
            Discover("search"),
            Discover("read-file"));

        await bootstrapper.InitializeAsync();

        Assert.Equal(["search"], registry.Handlers.Select(handler => handler.Name).ToArray());
    }

    [Fact]
    public async Task InitializeAsync_InvalidPolicy_ThrowsAndRegistersNothing()
    {
        var registry = new McpToolHandlerRegistry();
        var bootstrapper = CreateBootstrapper(
            registry,
            new McpServerPolicyOptions(),
            Discover("search"));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => bootstrapper.InitializeAsync());

        Assert.Contains("jeffymcp", ex.Message, StringComparison.Ordinal);
        Assert.Contains("AllowedTools", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(registry.Handlers);
    }

    [Fact]
    public async Task InitializeAsync_DiscoveredToolForUnknownServer_Throws()
    {
        var registry = new McpToolHandlerRegistry();
        var bootstrapper = CreateBootstrapper(
            registry,
            new McpServerPolicyOptions { AllowAllTools = true },
            new McpDiscoveredTool(
                "missing",
                "search",
                "search",
                "Search",
                """{"type":"object"}"""));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => bootstrapper.InitializeAsync());

        Assert.Contains("missing", ex.Message, StringComparison.Ordinal);
    }

    private static McpToolBootstrapper CreateBootstrapper(
        McpToolHandlerRegistry registry,
        McpServerPolicyOptions policy,
        params McpDiscoveredTool[] tools)
    {
        var options = Options.Create(new McpOptions
        {
            Servers =
            [
                new McpServerOptions
                {
                    Id = "jeffymcp",
                    Enabled = true,
                    Policy = policy
                }
            ]
        });

        return new McpToolBootstrapper(
            new FakeMcpConnectionManager(tools),
            registry,
            options,
            NullLogger<McpToolBootstrapper>.Instance);
    }

    private static McpDiscoveredTool Discover(string name) =>
        new("jeffymcp", name, name, name, """{"type":"object"}""");
}

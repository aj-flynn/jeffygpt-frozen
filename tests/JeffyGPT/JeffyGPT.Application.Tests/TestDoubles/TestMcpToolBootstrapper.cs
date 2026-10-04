using JeffyGPT.Application.Chat;
using JeffyGPT.Infrastructure.Mcp;

namespace JeffyGPT.Application.Tests.TestDoubles;

public sealed class TestMcpToolBootstrapper : IMcpToolBootstrapper
{
    private readonly McpToolHandlerRegistry _registry;

    public TestMcpToolBootstrapper(McpToolHandlerRegistry registry)
    {
        _registry = registry;
    }

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        _registry.Register(
        [
            new GetCurrentTimeToolHandler(),
            new EchoQueryToolHandler()
        ]);

        return Task.CompletedTask;
    }
}

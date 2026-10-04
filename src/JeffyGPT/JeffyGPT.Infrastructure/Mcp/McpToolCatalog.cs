using JeffyGPT.Application.Chat;
using JeffyGPT.Contracts.Chat;

namespace JeffyGPT.Infrastructure.Mcp;

public sealed class McpToolCatalog : IToolCatalog
{
    private readonly McpToolHandlerRegistry _registry;

    public McpToolCatalog(McpToolHandlerRegistry registry)
    {
        _registry = registry;
    }

    public IReadOnlyList<ToolDefinitionDto> GetTools() =>
        _registry.Handlers.Select(handler => handler.Definition).ToList();
}

using JeffyGPT.Application.Chat;

namespace JeffyGPT.Infrastructure.Mcp;

public sealed class McpToolHandlerRegistry
{
    private IReadOnlyList<IToolHandler>? _handlers;

    public IReadOnlyList<IToolHandler> Handlers => _handlers
        ?? throw new InvalidOperationException("Initialize MCP tools before resolving the tool executor or reading the catalog.");

    public void Register(IReadOnlyList<IToolHandler> handlers)
    {
        if (_handlers is not null)
        {
            throw new InvalidOperationException("MCP tools have already been initialized.");
        }

        _handlers = handlers.ToArray();
    }
}

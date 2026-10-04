using JeffyGPT.Application.Chat;
using JeffyGPT.Application.Chat.Tools;
using JeffyGPT.Contracts.Chat;

namespace JeffyGPT.Infrastructure.Mcp;

public sealed class McpToolHandlerRegistry
{
    private IReadOnlyList<IToolHandler> _handlers = [];

    public IReadOnlyList<IToolHandler> Handlers => _handlers;

    public void Register(IReadOnlyList<IToolHandler> handlers)
    {
        _handlers = handlers;
    }
}

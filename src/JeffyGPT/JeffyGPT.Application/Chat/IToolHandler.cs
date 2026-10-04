using JeffyGPT.Application.Chat.Tools;
using JeffyGPT.Contracts.Chat;

namespace JeffyGPT.Application.Chat;

public interface IToolHandler
{
    string Name { get; }

    ToolDefinitionDto Definition { get; }

    ToolSecurityDescriptor Security { get; }

    Task<ToolResultDto> ExecuteAsync(ToolCallDto toolCall, CancellationToken cancellationToken = default);
}

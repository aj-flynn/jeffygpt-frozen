using JeffyGPT.Contracts.Chat;

namespace JeffyGPT.Application.Chat;

public interface IToolExecutor
{
    Task<ToolResultDto> ExecuteAsync(ToolCallDto toolCall, CancellationToken cancellationToken = default);
}

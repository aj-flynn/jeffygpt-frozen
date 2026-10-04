using JeffyGPT.Contracts.Chat;

namespace JeffyGPT.Application.Chat;

public interface IToolCatalog
{
    IReadOnlyList<ToolDefinitionDto> GetTools();
}

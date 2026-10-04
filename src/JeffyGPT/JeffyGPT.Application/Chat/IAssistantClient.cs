using JeffyGPT.Contracts.Chat;

namespace JeffyGPT.Application.Chat;

public interface IAssistantClient
{
    Task<AssistantCompletion> CompleteAsync(
        IReadOnlyList<ChatMessageDto> messages,
        IReadOnlyList<ToolDefinitionDto> tools,
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<AssistantStreamPart> CompleteStreamingAsync(
        IReadOnlyList<ChatMessageDto> messages,
        IReadOnlyList<ToolDefinitionDto> tools,
        CancellationToken cancellationToken = default);
}

using JeffyGPT.Contracts.Chat;

namespace JeffyGPT.Application.Chat;

public interface IChatOrchestrator
{
    Task<ChatResponse> SendAsync(ChatRequest request, CancellationToken cancellationToken = default);

    IAsyncEnumerable<ChatStreamEvent> StreamAsync(
        ChatRequest request,
        CancellationToken cancellationToken = default);
}

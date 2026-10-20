using JeffyGPT.Contracts.Chat;

namespace JeffyGPT.Application.Chat;

public interface IChatOrchestrator
{
    /// <summary>
    /// Copies the saved transcript, appends the user text, and marks that conversation busy.
    /// A missing id creates a conversation. Read <see cref="ChatTurn.Events"/> to run the turn:
    /// a finished turn replaces the transcript, and any other exit discards the copy.
    /// </summary>
    ChatTurn StartTurn(string? conversationId, string? content);

    Task<ChatResponse> SendAsync(
        string? conversationId,
        string? content,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// The id to send on the next turn, and the events for this one.
/// </summary>
public sealed record ChatTurn(string ConversationId, IAsyncEnumerable<ChatStreamEvent> Events);

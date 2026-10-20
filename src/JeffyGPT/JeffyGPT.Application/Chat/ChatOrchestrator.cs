using System.Runtime.CompilerServices;
using JeffyGPT.Contracts.Chat;

namespace JeffyGPT.Application.Chat;

public class ChatOrchestrator : IChatOrchestrator
{
    public const int MaxUserCharacters = 32_000;
    public const int MaxStoredCharacters = 128_000;

    private const int MaxToolIterations = 40;

    private readonly IAssistantClient _assistantClient;
    private readonly IToolCatalog _toolCatalog;
    private readonly IToolExecutor _toolExecutor;
    private readonly Dictionary<Guid, Conversation> _conversations = new();
    private readonly object _gate = new();

    public ChatOrchestrator(
        IAssistantClient assistantClient,
        IToolCatalog toolCatalog,
        IToolExecutor toolExecutor)
    {
        _assistantClient = assistantClient;
        _toolCatalog = toolCatalog;
        _toolExecutor = toolExecutor;
    }

    public ChatTurn StartTurn(string? conversationId, string? content)
    {
        var text = RequireUserText(content);
        var (conversation, draft, id) = TakeDraft(conversationId, text);
        return new ChatTurn(id.ToString("D"), ReadEvents(conversation, draft));
    }

    public async Task<ChatResponse> SendAsync(
        string? conversationId,
        string? content,
        CancellationToken cancellationToken = default)
    {
        var turn = StartTurn(conversationId, content);
        ChatMessageDto? assistantMessage = null;
        var toolCalls = new List<ToolCallDto>();
        var toolResults = new List<ToolResultDto>();

        await foreach (var streamEvent in turn.Events.WithCancellation(cancellationToken))
        {
            switch (streamEvent)
            {
                case AssistantMessageEvent messageEvent:
                    assistantMessage = messageEvent.Message;
                    break;
                case ToolCallStartedEvent started:
                    toolCalls.Add(started.ToolCall);
                    break;
                case ToolCallFinishedEvent finished:
                    toolResults.Add(finished.ToolResult);
                    break;
                case StreamErrorEvent error:
                    throw new InvalidOperationException(
                        error.Content ?? "Chat stream failed without a completion.");
            }
        }

        if (assistantMessage is null)
        {
            throw new InvalidOperationException("Chat stream completed without an assistant message.");
        }

        return new ChatResponse(turn.ConversationId, assistantMessage, toolCalls, toolResults);
    }

    /// <summary>
    /// Under the gate, and never across an await.
    /// A blank id starts a conversation. Anything else must already be stored.
    /// Over the character bound throws and leaves the conversation idle.
    /// </summary>
    private (Conversation Conversation, List<ChatMessageDto> Draft, Guid Id) TakeDraft(
        string? conversationId,
        string text)
    {
        lock (_gate)
        {
            var isNew = string.IsNullOrWhiteSpace(conversationId);
            Conversation? conversation;
            Guid id;

            if (isNew)
            {
                id = Guid.NewGuid();
                conversation = new Conversation();
            }
            else if (!Guid.TryParseExact(conversationId, "D", out id)
                || id == Guid.Empty
                || !_conversations.TryGetValue(id, out conversation))
            {
                throw new ConversationNotFoundException();
            }

            if (conversation.Busy)
            {
                throw new ConversationBusyException();
            }

            if (StoredLength(conversation.Messages) + text.Length > MaxStoredCharacters)
            {
                throw new ArgumentException(
                    $"The conversation has reached its {MaxStoredCharacters} character limit.");
            }

            if (isNew)
            {
                _conversations.Add(id, conversation);
            }

            conversation.Busy = true;
            var draft = conversation.Messages.ToList();
            draft.Add(new ChatMessageDto("user", text));
            return (conversation, draft, id);
        }
    }

    private async IAsyncEnumerable<ChatStreamEvent> ReadEvents(
        Conversation conversation,
        List<ChatMessageDto> draft,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var save = false;
        try
        {
            var tools = _toolCatalog.GetTools();
            var toolRoundsExecuted = 0;

            while (true)
            {
                AssistantCompletion? completion = null;
                var modelInput = JeffyPersonality.WithSystemPrompt(draft);

                await foreach (var part in _assistantClient.CompleteStreamingAsync(
                    modelInput,
                    tools,
                    cancellationToken))
                {
                    switch (part)
                    {
                        case AssistantContentDelta delta:
                            yield return new AssistantDeltaEvent(delta.Content);
                            break;
                        case AssistantCompletionFinished finished:
                            completion = finished.Completion;
                            break;
                    }
                }

                if (completion is null)
                {
                    yield return new StreamErrorEvent("Assistant stream ended without a completion.");
                    yield break;
                }

                if (IsFinalAssistantTurn(completion))
                {
                    draft.Add(completion.AssistantMessage);
                    save = true;
                    yield return new AssistantMessageEvent(completion.AssistantMessage);
                    yield return new StreamDoneEvent();
                    yield break;
                }

                if (HasReachedToolRoundLimit(toolRoundsExecuted))
                {
                    var limitMessage = BuildToolRoundLimitMessage();
                    draft.Add(limitMessage);
                    save = true;
                    yield return new AssistantMessageEvent(limitMessage);
                    yield return new StreamDoneEvent();
                    yield break;
                }

                AppendAssistantWithToolCalls(draft, completion);

                yield return new AssistantToolRoundEvent(
                    new ChatMessageDto(
                        completion.AssistantMessage.Role,
                        completion.AssistantMessage.Content,
                        completion.ToolCalls));

                foreach (var toolCall in completion.ToolCalls)
                {
                    yield return new ToolCallStartedEvent(toolCall);

                    var toolResult = await ExecuteAndAppendToolResultAsync(
                        draft,
                        toolCall,
                        cancellationToken);

                    yield return new ToolCallFinishedEvent(toolResult);
                }

                toolRoundsExecuted++;
            }
        }
        finally
        {
            lock (_gate)
            {
                if (save)
                {
                    conversation.Messages = WithinStoredLimit(draft);
                }

                conversation.Busy = false;
            }
        }
    }

    private static string RequireUserText(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new ArgumentException("A user message is required.");
        }

        var trimmed = content.Trim();
        if (trimmed.Length > MaxUserCharacters)
        {
            throw new ArgumentException(
                $"Each message content must be at most {MaxUserCharacters} characters.");
        }

        return trimmed;
    }

    private static List<ChatMessageDto> WithinStoredLimit(List<ChatMessageDto> draft)
    {
        if (StoredLength(draft) <= MaxStoredCharacters)
        {
            return draft;
        }

        var kept = new List<ChatMessageDto>();
        var used = 0;
        for (var index = draft.Count - 1; index >= 0; index--)
        {
            var length = draft[index].Content.Length;
            if (used + length > MaxStoredCharacters)
            {
                continue;
            }

            kept.Add(draft[index]);
            used += length;
        }

        kept.Reverse();
        return kept;
    }

    private static int StoredLength(IReadOnlyList<ChatMessageDto> messages)
    {
        var total = 0;
        foreach (var message in messages)
        {
            total += message.Content.Length;
        }

        return total;
    }

    private static ChatMessageDto BuildToolRoundLimitMessage() =>
        new(
            "assistant",
            $"The tool loop stopped after the maximum of {MaxToolIterations} tool rounds.");

    private static bool IsFinalAssistantTurn(AssistantCompletion completion) =>
        completion.ToolCalls.Count == 0;

    private static bool HasReachedToolRoundLimit(int toolRoundsExecuted) =>
        toolRoundsExecuted >= MaxToolIterations;

    private static void AppendAssistantWithToolCalls(
        List<ChatMessageDto> conversation,
        AssistantCompletion completion)
    {
        conversation.Add(new ChatMessageDto(
            completion.AssistantMessage.Role,
            completion.AssistantMessage.Content,
            completion.ToolCalls));
    }

    private async Task<ToolResultDto> ExecuteAndAppendToolResultAsync(
        List<ChatMessageDto> conversation,
        ToolCallDto toolCall,
        CancellationToken cancellationToken)
    {
        var toolResult = await _toolExecutor.ExecuteAsync(toolCall, cancellationToken);
        conversation.Add(new ChatMessageDto(
            "tool",
            toolResult.ContentJson,
            ToolCallId: toolResult.ToolCallId));
        return toolResult;
    }

    private sealed class Conversation
    {
        public List<ChatMessageDto> Messages { get; set; } = [];
        public bool Busy { get; set; }
    }
}

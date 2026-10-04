using System.Runtime.CompilerServices;
using JeffyGPT.Contracts.Chat;

namespace JeffyGPT.Application.Chat;

public class ChatOrchestrator : IChatOrchestrator
{
    private const int MaxToolIterations = 40;

    private readonly IAssistantClient _assistantClient;
    private readonly IToolCatalog _toolCatalog;
    private readonly IToolExecutor _toolExecutor;

    public ChatOrchestrator(
        IAssistantClient assistantClient,
        IToolCatalog toolCatalog,
        IToolExecutor toolExecutor)
    {
        _assistantClient = assistantClient;
        _toolCatalog = toolCatalog;
        _toolExecutor = toolExecutor;
    }

    /// <summary>
    /// Collects the final chat response from <see cref="StreamAsync"/>.
    /// Content deltas and tool-round events are not part of the sync response.
    /// </summary>
    public async Task<ChatResponse> SendAsync(ChatRequest request, CancellationToken cancellationToken = default)
    {
        return await CollectChatResponseAsync(request, cancellationToken);
    }

    public async IAsyncEnumerable<ChatStreamEvent> StreamAsync(
        ChatRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ChatRequestValidator.Validate(request);

        var tools = _toolCatalog.GetTools();
        var conversation = JeffyPersonality.WithSystemPrompt(request.Messages);
        var toolRoundsExecuted = 0;

        while (true)
        {
            AssistantCompletion? completion = null;

            await foreach (var part in _assistantClient.CompleteStreamingAsync(conversation, tools, cancellationToken))
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
                yield return new AssistantMessageEvent(completion.AssistantMessage);
                yield return new StreamDoneEvent();
                yield break;
            }

            if (HasReachedToolRoundLimit(toolRoundsExecuted))
            {
                yield return new AssistantMessageEvent(BuildToolRoundLimitMessage());
                yield return new StreamDoneEvent();
                yield break;
            }

            AppendAssistantWithToolCalls(conversation, completion);

            yield return new AssistantToolRoundEvent(
                new ChatMessageDto(
                    completion.AssistantMessage.Role,
                    completion.AssistantMessage.Content,
                    completion.ToolCalls));

            foreach (var toolCall in completion.ToolCalls)
            {
                yield return new ToolCallStartedEvent(toolCall);

                var toolResult = await ExecuteAndAppendToolResultAsync(
                    conversation,
                    toolCall,
                    cancellationToken);

                yield return new ToolCallFinishedEvent(toolResult);
            }

            toolRoundsExecuted++;
        }
    }

    private async Task<ChatResponse> CollectChatResponseAsync(
        ChatRequest request,
        CancellationToken cancellationToken)
    {
        ChatMessageDto? assistantMessage = null;
        var toolCalls = new List<ToolCallDto>();
        var toolResults = new List<ToolResultDto>();

        await foreach (var streamEvent in StreamAsync(request, cancellationToken))
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

        return new ChatResponse(assistantMessage, toolCalls, toolResults);
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
}

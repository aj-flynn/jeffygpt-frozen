using System.Runtime.CompilerServices;
using JeffyGPT.Application.Chat;
using JeffyGPT.Contracts.Chat;

namespace JeffyGPT.Application.Tests.TestDoubles;

internal sealed class StubAssistantClient : IAssistantClient
{
    public List<IReadOnlyList<ChatMessageDto>> Calls { get; } = [];

    public Queue<AssistantCompletion> Responses { get; init; } = new();

    public Task<AssistantCompletion> CompleteAsync(
        IReadOnlyList<ChatMessageDto> messages,
        IReadOnlyList<ToolDefinitionDto> tools,
        CancellationToken cancellationToken = default)
    {
        _ = tools;
        cancellationToken.ThrowIfCancellationRequested();

        Calls.Add(messages.ToList());

        if (Responses.Count == 0)
        {
            throw new InvalidOperationException("No stubbed assistant responses remain.");
        }

        return Task.FromResult(Responses.Dequeue());
    }

    public async IAsyncEnumerable<AssistantStreamPart> CompleteStreamingAsync(
        IReadOnlyList<ChatMessageDto> messages,
        IReadOnlyList<ToolDefinitionDto> tools,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var completion = await CompleteAsync(messages, tools, cancellationToken);
        yield return new AssistantCompletionFinished(completion);
    }
}

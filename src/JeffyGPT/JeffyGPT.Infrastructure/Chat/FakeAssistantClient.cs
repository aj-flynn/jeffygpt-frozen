using System.Runtime.CompilerServices;
using System.Text.Json;
using JeffyGPT.Application.Chat;
using JeffyGPT.Contracts.Chat;

namespace JeffyGPT.Infrastructure.Chat;

public class FakeAssistantClient : IAssistantClient
{
    private const int ChunkSize = 6;

    public Task<AssistantCompletion> CompleteAsync(
        IReadOnlyList<ChatMessageDto> messages,
        IReadOnlyList<ToolDefinitionDto> tools,
        CancellationToken cancellationToken = default)
    {
        _ = tools;
        return Task.FromResult(BuildCompletion(messages));
    }

    public async IAsyncEnumerable<AssistantStreamPart> CompleteStreamingAsync(
        IReadOnlyList<ChatMessageDto> messages,
        IReadOnlyList<ToolDefinitionDto> tools,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        _ = tools;

        var completion = BuildCompletion(messages);

        if (completion.ToolCalls.Count > 0)
        {
            yield return new AssistantCompletionFinished(completion);
            yield break;
        }

        foreach (var chunk in ChunkContent(completion.AssistantMessage.Content))
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return new AssistantContentDelta(chunk);
        }

        yield return new AssistantCompletionFinished(completion);
    }

    private static AssistantCompletion BuildCompletion(IReadOnlyList<ChatMessageDto> messages)
    {
        var lastUserIndex = FindLastIndexByRole(messages, "user");
        var lastToolIndex = FindLastIndexByRole(messages, "tool");

        if (lastToolIndex > lastUserIndex)
        {
            var toolMessage = messages[lastToolIndex];
            try
            {
                using var document = JsonDocument.Parse(toolMessage.Content);
                if (document.RootElement.TryGetProperty("local", out var localElement))
                {
                    var local = localElement.GetString() ?? toolMessage.Content;
                    return new AssistantCompletion(
                        new ChatMessageDto("assistant", $"The current local time is {local}."),
                        []);
                }

                return new AssistantCompletion(
                    new ChatMessageDto("assistant", $"Tool result: {toolMessage.Content}"),
                    []);
            }
            catch (JsonException)
            {
                return new AssistantCompletion(
                    new ChatMessageDto("assistant", "Tool result was not valid JSON."),
                    []);
            }
        }

        var lastUserMessage = messages[lastUserIndex];

        if (TryBuildFakeToolCallCompletion(lastUserMessage.Content, out var toolCallCompletion))
        {
            return toolCallCompletion;
        }

        var assistantMessage = new ChatMessageDto("assistant", $"Echo: {lastUserMessage.Content}");
        return new AssistantCompletion(assistantMessage, []);
    }

    private static int FindLastIndexByRole(IReadOnlyList<ChatMessageDto> messages, string role)
    {
        for (var index = messages.Count - 1; index >= 0; index--)
        {
            if (string.Equals(messages[index].Role, role, StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return -1;
    }

    private static bool TryBuildFakeToolCallCompletion(string userContent, out AssistantCompletion completion)
    {
        if (userContent.Contains("read docs overview", StringComparison.OrdinalIgnoreCase))
        {
            var toolCalls = new List<ToolCallDto>
            {
                new(
                    "call_read_file",
                    "read-file",
                    """{"rootAlias":"docs","relativePath":"overview.md"}""")
            };

            completion = new AssistantCompletion(
                new ChatMessageDto("assistant", string.Empty, toolCalls),
                toolCalls);
            return true;
        }

        if (userContent.Contains("time", StringComparison.OrdinalIgnoreCase))
        {
            var toolCalls = new List<ToolCallDto>
            {
                new("call_get_current_time", "get-current-time", "{}")
            };

            completion = new AssistantCompletion(
                new ChatMessageDto("assistant", string.Empty, toolCalls),
                toolCalls);
            return true;
        }

        completion = null!;
        return false;
    }

    private static IEnumerable<string> ChunkContent(string content)
    {
        for (var index = 0; index < content.Length; index += ChunkSize)
        {
            var length = Math.Min(ChunkSize, content.Length - index);
            yield return content.Substring(index, length);
        }
    }
}

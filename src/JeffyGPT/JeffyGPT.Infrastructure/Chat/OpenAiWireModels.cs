using System.Text.Json;
using System.Text.Json.Serialization;

namespace JeffyGPT.Infrastructure.Chat;

// Wire-format models for the OpenAI-compatible chat/completions endpoint.
// These mirror the provider JSON exactly and never leave the Infrastructure layer.

internal sealed class ChatCompletionRequest
{
    public required string Model { get; init; }

    public required IReadOnlyList<ChatCompletionMessage> Messages { get; init; }

    public IReadOnlyList<ChatCompletionTool>? Tools { get; init; }

    [JsonPropertyName("tool_choice")]
    public required string ToolChoice { get; init; }

    [JsonPropertyName("reasoning_effort")]
    public string? ReasoningEffort { get; init; }

    public bool Stream { get; init; }
}

internal sealed class ChatCompletionMessage
{
    public required string Role { get; init; }

    public string? Content { get; init; }

    [JsonPropertyName("tool_calls")]
    public IReadOnlyList<ChatCompletionToolCall>? ToolCalls { get; init; }

    [JsonPropertyName("tool_call_id")]
    public string? ToolCallId { get; init; }
}

internal sealed class ChatCompletionTool
{
    public required string Type { get; init; }

    public required ChatCompletionFunction Function { get; init; }
}

internal sealed class ChatCompletionFunction
{
    public required string Name { get; init; }

    public required string Description { get; init; }

    public required JsonElement Parameters { get; init; }
}

internal sealed class ChatCompletionResponse
{
    public IReadOnlyList<ChatCompletionChoice>? Choices { get; init; }
}

internal sealed class ChatCompletionChoice
{
    public ChatCompletionResponseMessage? Message { get; init; }
}

internal sealed class ChatCompletionResponseMessage
{
    public string? Content { get; init; }

    [JsonPropertyName("tool_calls")]
    public IReadOnlyList<ChatCompletionToolCall>? ToolCalls { get; init; }
}

internal sealed class ChatCompletionStreamChunk
{
    public IReadOnlyList<ChatCompletionStreamChoice>? Choices { get; init; }
}

internal sealed class ChatCompletionStreamChoice
{
    public ChatCompletionStreamDelta? Delta { get; init; }
}

internal sealed class ChatCompletionStreamDelta
{
    public string? Content { get; init; }

    [JsonPropertyName("tool_calls")]
    public IReadOnlyList<ChatCompletionStreamToolCall>? ToolCalls { get; init; }
}

internal sealed class ChatCompletionStreamToolCall
{
    public int Index { get; init; }

    public string? Id { get; init; }

    public ChatCompletionStreamToolCallFunction? Function { get; init; }
}

internal sealed class ChatCompletionStreamToolCallFunction
{
    public string? Name { get; init; }

    public string? Arguments { get; init; }
}

internal sealed class ChatCompletionToolCall
{
    public required string Id { get; init; }

    public required string Type { get; init; }

    public required ChatCompletionToolCallFunction Function { get; init; }
}

internal sealed class ChatCompletionToolCallFunction
{
    public required string Name { get; init; }

    public string? Arguments { get; init; }
}

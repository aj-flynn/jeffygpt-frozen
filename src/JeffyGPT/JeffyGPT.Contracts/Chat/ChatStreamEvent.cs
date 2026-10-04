using System.Text.Json.Serialization;

namespace JeffyGPT.Contracts.Chat;

/// <summary>
/// One NDJSON event on the chat stream. Serialized with a "type" discriminator so the
/// wire shape stays {"type":"...", ...payload}; see the frontend parser in chatStreamParse.ts.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(AssistantDeltaEvent), "assistant_delta")]
[JsonDerivedType(typeof(AssistantToolRoundEvent), "assistant_tool_round")]
[JsonDerivedType(typeof(ToolCallStartedEvent), "tool_call_started")]
[JsonDerivedType(typeof(ToolCallFinishedEvent), "tool_call_finished")]
[JsonDerivedType(typeof(AssistantMessageEvent), "assistant_message")]
[JsonDerivedType(typeof(StreamErrorEvent), "error")]
[JsonDerivedType(typeof(StreamDoneEvent), "done")]
public abstract record ChatStreamEvent;

public sealed record AssistantDeltaEvent(string Content) : ChatStreamEvent;

public sealed record AssistantToolRoundEvent(ChatMessageDto Message) : ChatStreamEvent;

public sealed record ToolCallStartedEvent(ToolCallDto ToolCall) : ChatStreamEvent;

public sealed record ToolCallFinishedEvent(ToolResultDto ToolResult) : ChatStreamEvent;

public sealed record AssistantMessageEvent(ChatMessageDto Message) : ChatStreamEvent;

public sealed record StreamErrorEvent(string Content) : ChatStreamEvent;

public sealed record StreamDoneEvent : ChatStreamEvent;

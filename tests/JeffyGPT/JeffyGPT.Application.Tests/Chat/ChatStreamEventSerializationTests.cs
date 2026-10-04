using System.Text.Json;
using JeffyGPT.Contracts.Chat;

namespace JeffyGPT.Application.Tests.Chat;

/// <summary>
/// Guards the NDJSON wire shape consumed by the frontend parser (chatStreamParse.ts):
/// a "type" discriminator followed by a single camelCase payload property, nulls omitted.
/// </summary>
public class ChatStreamEventSerializationTests
{
    private static string Serialize(ChatStreamEvent streamEvent) =>
        JsonSerializer.Serialize(streamEvent, ChatStreamJson.WireOptions);

    [Fact]
    public void AssistantDelta_SerializesTypeAndContent()
    {
        var json = Serialize(new AssistantDeltaEvent("Hello"));

        Assert.Equal("""{"type":"assistant_delta","content":"Hello"}""", json);
    }

    [Fact]
    public void AssistantMessage_SerializesTypeAndMessageWithoutNulls()
    {
        var json = Serialize(new AssistantMessageEvent(new ChatMessageDto("assistant", "Hi")));

        Assert.Equal("""{"type":"assistant_message","message":{"role":"assistant","content":"Hi"}}""", json);
    }

    [Fact]
    public void AssistantToolRound_SerializesMessageWithToolCalls()
    {
        var message = new ChatMessageDto(
            "assistant",
            string.Empty,
            [new ToolCallDto("call_1", "get-current-time", "{}")]);

        var json = Serialize(new AssistantToolRoundEvent(message));

        Assert.Equal(
            """{"type":"assistant_tool_round","message":{"role":"assistant","content":"","toolCalls":[{"id":"call_1","name":"get-current-time","argumentsJson":"{}"}]}}""",
            json);
    }

    [Fact]
    public void ToolCallStarted_SerializesTypeAndToolCall()
    {
        var json = Serialize(new ToolCallStartedEvent(new ToolCallDto("call_1", "get-current-time", "{}")));

        Assert.Equal(
            """{"type":"tool_call_started","toolCall":{"id":"call_1","name":"get-current-time","argumentsJson":"{}"}}""",
            json);
    }

    [Fact]
    public void ToolCallFinished_SerializesTypeAndToolResult()
    {
        var json = Serialize(new ToolCallFinishedEvent(new ToolResultDto("call_1", "{}", IsError: false)));

        Assert.Equal(
            """{"type":"tool_call_finished","toolResult":{"toolCallId":"call_1","contentJson":"{}","isError":false}}""",
            json);
    }

    [Fact]
    public void Error_SerializesTypeAndContent()
    {
        var json = Serialize(new StreamErrorEvent("boom"));

        Assert.Equal("""{"type":"error","content":"boom"}""", json);
    }

    [Fact]
    public void Done_SerializesTypeOnly()
    {
        var json = Serialize(new StreamDoneEvent());

        Assert.Equal("""{"type":"done"}""", json);
    }

    [Fact]
    public void Deserialize_RoundTripsToDerivedTypes()
    {
        var line = """{"type":"assistant_delta","content":"Hello"}""";

        var streamEvent = JsonSerializer.Deserialize<ChatStreamEvent>(line, ChatStreamJson.WireOptions);

        var delta = Assert.IsType<AssistantDeltaEvent>(streamEvent);
        Assert.Equal("Hello", delta.Content);
    }
}

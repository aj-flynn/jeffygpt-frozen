using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using JeffyGPT.Contracts.Chat;
using Microsoft.AspNetCore.Mvc.Testing;

namespace JeffyGPT.Api.Tests;

public class ChatEndpointTests : IClassFixture<JeffyGPTWebApplicationFactory>
{
    private readonly HttpClient _client;

    public ChatEndpointTests(JeffyGPTWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task PostChat_EmptyMessages_ReturnsBadRequest()
    {
        var request = new ChatRequest([]);

        var response = await _client.PostAsJsonAsync("/api/chat", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostChat_NormalMessage_ReturnsEchoResponse()
    {
        var request = new ChatRequest(
        [
            new ChatMessageDto("user", "hello there")
        ]);

        var response = await _client.PostAsJsonAsync("/api/chat", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ChatResponse>();
        Assert.NotNull(body);
        Assert.Equal("assistant", body.AssistantMessage.Role);
        Assert.Equal("Echo: hello there", body.AssistantMessage.Content);
        Assert.Empty(body.ToolCalls);
        Assert.Empty(body.ToolResults);
    }

    [Fact]
    public async Task PostChat_MessageContainingTime_ReturnsFakeToolCallAndResult()
    {
        var request = new ChatRequest(
        [
            new ChatMessageDto("user", "what time is it")
        ]);

        var response = await _client.PostAsJsonAsync("/api/chat", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ChatResponse>();
        Assert.NotNull(body);
        Assert.Equal("assistant", body.AssistantMessage.Role);
        Assert.StartsWith("The current local time is ", body.AssistantMessage.Content);
        Assert.EndsWith(".", body.AssistantMessage.Content);

        var toolCall = Assert.Single(body.ToolCalls);
        Assert.Equal("get-current-time", toolCall.Name);
        Assert.Equal("{}", toolCall.ArgumentsJson);

        var toolResult = Assert.Single(body.ToolResults);
        Assert.Equal(toolCall.Id, toolResult.ToolCallId);
        Assert.False(toolResult.IsError);
        Assert.Contains("local", toolResult.ContentJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PostChatStream_EmptyMessages_ReturnsBadRequest()
    {
        var request = new ChatRequest([]);

        var response = await _client.PostAsJsonAsync("/api/chat/stream", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostChatStream_NormalMessage_ReturnsNdjsonStream()
    {
        var request = new ChatRequest(
        [
            new ChatMessageDto("user", "hello there")
        ]);

        var response = await _client.PostAsJsonAsync("/api/chat/stream", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/x-ndjson", response.Content.Headers.ContentType?.MediaType);

        var events = await ReadNdjsonStreamAsync(response);
        Assert.Contains(events, e => e is AssistantDeltaEvent);
        Assert.Equal("Echo: hello there", string.Concat(
            events.OfType<AssistantDeltaEvent>().Select(e => e.Content)));

        var assistantMessage = Assert.Single(events.OfType<AssistantMessageEvent>());
        Assert.Equal("Echo: hello there", assistantMessage.Message.Content);
        Assert.Contains(events, e => e is StreamDoneEvent);
    }

    [Fact]
    public async Task PostChatStream_MessageContainingTime_IncludesToolEvents()
    {
        var request = new ChatRequest(
        [
            new ChatMessageDto("user", "what time is it")
        ]);

        var response = await _client.PostAsJsonAsync("/api/chat/stream", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var events = await ReadNdjsonStreamAsync(response);

        var toolRoundIndex = events.FindIndex(e => e is AssistantToolRoundEvent);
        var toolStartedIndex = events.FindIndex(e => e is ToolCallStartedEvent);
        Assert.True(toolRoundIndex >= 0);
        Assert.True(toolStartedIndex >= 0);
        Assert.True(toolRoundIndex < toolStartedIndex);

        Assert.Contains(events, e => e is ToolCallStartedEvent);
        Assert.Contains(events, e => e is ToolCallFinishedEvent);
        Assert.Contains(events, e => e is AssistantMessageEvent);
        Assert.Contains(events, e => e is StreamDoneEvent);
    }

    private static async Task<List<ChatStreamEvent>> ReadNdjsonStreamAsync(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        var events = new List<ChatStreamEvent>();

        foreach (var line in json.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var streamEvent = JsonSerializer.Deserialize<ChatStreamEvent>(line, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            if (streamEvent is not null)
            {
                events.Add(streamEvent);
            }
        }

        return events;
    }
}

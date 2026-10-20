using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using JeffyGPT.Api.Endpoints;
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
    public async Task PostChat_WithoutId_ReturnsEchoAndAConversationId()
    {
        var response = await _client.PostAsJsonAsync("/api/chat", new ChatTurnRequest(null, "hello there"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ChatResponse>();
        Assert.NotNull(body);
        Assert.Equal("Echo: hello there", body.AssistantMessage.Content);
        Assert.NotEqual(Guid.Empty, Guid.ParseExact(body.ConversationId, "D"));
    }

    [Fact]
    public async Task PostChat_UnknownId_ReturnsNotFound()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/chat",
            new ChatTurnRequest(Guid.NewGuid().ToString("D"), "hello"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task PostChat_EmptyContent_ReturnsBadRequest()
    {
        var response = await _client.PostAsJsonAsync("/api/chat", new ChatTurnRequest(null, "  "));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostChat_SecondTurn_AnswersTheNewMessageOnTheSameConversation()
    {
        var first = await _client.PostAsJsonAsync("/api/chat", new ChatTurnRequest(null, "what time is it"));
        var started = await first.Content.ReadFromJsonAsync<ChatResponse>();
        Assert.NotNull(started);
        Assert.StartsWith("The current local time is ", started.AssistantMessage.Content);

        var second = await _client.PostAsJsonAsync(
            "/api/chat",
            new ChatTurnRequest(started.ConversationId, "hello"));

        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var body = await second.Content.ReadFromJsonAsync<ChatResponse>();
        Assert.NotNull(body);
        Assert.Equal("Echo: hello", body.AssistantMessage.Content);
        Assert.Equal(started.ConversationId, body.ConversationId);
    }

    [Fact]
    public async Task PostChatStream_ReturnsNdjsonAndTheConversationIdHeader()
    {
        var response = await _client.PostAsJsonAsync("/api/chat/stream", new ChatTurnRequest(null, "hello there"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/x-ndjson", response.Content.Headers.ContentType?.MediaType);
        var conversationId = Assert.Single(response.Headers.GetValues(ChatEndpoints.ConversationIdHeader));
        Assert.NotEqual(Guid.Empty, Guid.ParseExact(conversationId, "D"));

        var events = await ReadNdjsonStreamAsync(response);
        Assert.Equal("Echo: hello there", string.Concat(
            events.OfType<AssistantDeltaEvent>().Select(e => e.Content)));
        Assert.Contains(events, e => e is StreamDoneEvent);
    }

    [Fact]
    public async Task PostChatStream_MalformedId_ReturnsNotFound()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/chat/stream",
            new ChatTurnRequest("not-a-guid", "hello"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static async Task<List<ChatStreamEvent>> ReadNdjsonStreamAsync(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        var events = new List<ChatStreamEvent>();

        foreach (var line in json.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var streamEvent = JsonSerializer.Deserialize<ChatStreamEvent>(line, ChatStreamJson.WireOptions);
            if (streamEvent is not null)
            {
                events.Add(streamEvent);
            }
        }

        return events;
    }
}

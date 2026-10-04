using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using JeffyGPT.Application.Chat;
using JeffyGPT.Contracts.Chat;
using JeffyGPT.Infrastructure.Chat;
using JeffyGPT.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace JeffyGPT.Application.Tests;

public class OpenAiCompatibleAssistantClientTests
{
    [Fact]
    public async Task CompleteAsync_SendsOpenAiCompatibleChatCompletionRequest()
    {
        var handler = new RecordingHttpMessageHandler(_ => CreateAssistantContentResponse("Hello from the model."));
        var client = CreateClient(handler, "gpt-5.4-nano", "test-api-key");

        var messages = new List<ChatMessageDto>
        {
            new("user", "hello")
        };
        var tools = new List<ToolDefinitionDto>
        {
            new(
                "get-current-time",
                "Gets the current UTC time.",
                """{"type":"object","properties":{},"additionalProperties":false}""")
        };

        var completion = await client.CompleteAsync(messages, tools, CancellationToken.None);

        Assert.NotNull(handler.LastRequest);
        Assert.Equal(HttpMethod.Post, handler.LastRequest!.Method);
        Assert.Equal(new Uri("https://api.openai.com/v1/chat/completions"), handler.LastRequest.RequestUri);

        var authorization = handler.LastRequest.Headers.Authorization;
        Assert.NotNull(authorization);
        Assert.Equal("Bearer", authorization!.Scheme);
        Assert.Equal("test-api-key", authorization.Parameter);

        using var requestJson = JsonDocument.Parse(handler.LastRequestBody!);
        var root = requestJson.RootElement;

        Assert.Equal("gpt-5.4-nano", root.GetProperty("model").GetString());
        Assert.Equal("auto", root.GetProperty("tool_choice").GetString());
        Assert.False(root.TryGetProperty("reasoning_effort", out _));

        var requestMessages = root.GetProperty("messages");
        Assert.Equal(1, requestMessages.GetArrayLength());
        Assert.Equal("user", requestMessages[0].GetProperty("role").GetString());
        Assert.Equal("hello", requestMessages[0].GetProperty("content").GetString());

        var requestTools = root.GetProperty("tools");
        Assert.Equal(1, requestTools.GetArrayLength());
        Assert.Equal("function", requestTools[0].GetProperty("type").GetString());

        var function = requestTools[0].GetProperty("function");
        Assert.Equal("get-current-time", function.GetProperty("name").GetString());
        Assert.Equal("Gets the current UTC time.", function.GetProperty("description").GetString());
        Assert.Equal(JsonValueKind.Object, function.GetProperty("parameters").ValueKind);

        Assert.Equal("assistant", completion.AssistantMessage.Role);
        Assert.Equal("Hello from the model.", completion.AssistantMessage.Content);
        Assert.Empty(completion.ToolCalls);
    }

    [Fact]
    public async Task CompleteAsync_WhenReasoningEffortConfigured_SendsReasoningEffort()
    {
        var handler = new RecordingHttpMessageHandler(_ => CreateAssistantContentResponse("ok"));
        var client = CreateClient(handler, "gpt-5.6-luna", "test-api-key", reasoningEffort: "none");

        await client.CompleteAsync(
            [new ChatMessageDto("user", "hello")],
            [],
            CancellationToken.None);

        using var requestJson = JsonDocument.Parse(handler.LastRequestBody!);
        Assert.Equal("none", requestJson.RootElement.GetProperty("reasoning_effort").GetString());
    }

    [Fact]
    public async Task CompleteAsync_HttpError_ThrowsAssistantClientExceptionWithStatus()
    {
        var handler = new RecordingHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent("""{"error":"invalid api key"}""")
        });
        var client = CreateClient(handler, "gpt-5.4-nano", "test-api-key");

        var exception = await Assert.ThrowsAsync<AssistantClientException>(() =>
            client.CompleteAsync(
                [new ChatMessageDto("user", "hello")],
                [],
                CancellationToken.None));

        Assert.Contains("401", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("invalid api key", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CompleteAsync_MapsReturnedToolCalls()
    {
        var handler = new RecordingHttpMessageHandler(_ => CreateToolCallResponse(
            "call_get-current-time",
            "get-current-time",
            """{"timezone":"UTC"}"""));
        var client = CreateClient(handler, "gpt-5.4-nano", "test-api-key");

        var completion = await client.CompleteAsync(
            [new ChatMessageDto("user", "what time is it")],
            [],
            CancellationToken.None);

        Assert.Equal(string.Empty, completion.AssistantMessage.Content);

        var toolCall = Assert.Single(completion.ToolCalls);
        Assert.Equal("call_get-current-time", toolCall.Id);
        Assert.Equal("get-current-time", toolCall.Name);
        Assert.Equal("""{"timezone":"UTC"}""", toolCall.ArgumentsJson);
    }

    [Fact]
    public async Task CompleteAsync_SendsAssistantToolCallsAndToolResultsInFollowUpRequest()
    {
        var handler = new RecordingHttpMessageHandler(_ =>
            CreateAssistantContentResponse("The current UTC time is 2026-06-27T12:00:00Z."));
        var client = CreateClient(handler, "gpt-5.4-nano", "test-api-key");

        var completion = await client.CompleteAsync(
            [
                new ChatMessageDto("user", "what time is it"),
                new ChatMessageDto(
                    "assistant",
                    string.Empty,
                    [new ToolCallDto("call_get-current-time", "get-current-time", "{}")]),
                new ChatMessageDto(
                    "tool",
                    """{"utc":"2026-06-27T12:00:00Z"}""",
                    ToolCallId: "call_get-current-time")
            ],
            [],
            CancellationToken.None);

        using var requestJson = JsonDocument.Parse(handler.LastRequestBody!);
        var requestMessages = requestJson.RootElement.GetProperty("messages");
        Assert.Equal(3, requestMessages.GetArrayLength());

        var assistantMessage = requestMessages[1];
        Assert.Equal("assistant", assistantMessage.GetProperty("role").GetString());
        Assert.False(assistantMessage.TryGetProperty("content", out _));
        var toolCalls = assistantMessage.GetProperty("tool_calls");
        Assert.Equal(1, toolCalls.GetArrayLength());
        Assert.Equal("call_get-current-time", toolCalls[0].GetProperty("id").GetString());

        var toolMessage = requestMessages[2];
        Assert.Equal("tool", toolMessage.GetProperty("role").GetString());
        Assert.Equal("call_get-current-time", toolMessage.GetProperty("tool_call_id").GetString());
        Assert.Equal("""{"utc":"2026-06-27T12:00:00Z"}""", toolMessage.GetProperty("content").GetString());

        Assert.Equal("The current UTC time is 2026-06-27T12:00:00Z.", completion.AssistantMessage.Content);
        Assert.Empty(completion.ToolCalls);
    }

    [Fact]
    public async Task CompleteStreamingAsync_SendsStreamTrueAndMapsContentDeltas()
    {
        var handler = new RecordingHttpMessageHandler(_ => CreateStreamingContentResponse(
            "Hello ",
            "from ",
            "the model."));
        var client = CreateClient(handler, "gpt-5.4-nano", "test-api-key");

        var parts = new List<AssistantStreamPart>();
        await foreach (var part in client.CompleteStreamingAsync(
            [new ChatMessageDto("user", "hello")],
            [],
            CancellationToken.None))
        {
            parts.Add(part);
        }

        using var requestJson = JsonDocument.Parse(handler.LastRequestBody!);
        Assert.True(requestJson.RootElement.GetProperty("stream").GetBoolean());

        var deltas = parts.OfType<AssistantContentDelta>().Select(part => part.Content).ToList();
        Assert.Equal(["Hello ", "from ", "the model."], deltas);

        var finished = Assert.IsType<AssistantCompletionFinished>(parts.Last());
        Assert.Equal("Hello from the model.", finished.Completion.AssistantMessage.Content);
        Assert.Empty(finished.Completion.ToolCalls);
    }

    [Fact]
    public async Task CompleteStreamingAsync_AssemblesStreamingToolCalls()
    {
        var handler = new RecordingHttpMessageHandler(_ => CreateStreamingToolCallResponse(
            "call_get-current-time",
            "get-current-time",
            """{"timezone":"UTC"}"""));
        var client = CreateClient(handler, "gpt-5.4-nano", "test-api-key");

        var parts = new List<AssistantStreamPart>();
        await foreach (var part in client.CompleteStreamingAsync(
            [new ChatMessageDto("user", "what time is it")],
            [],
            CancellationToken.None))
        {
            parts.Add(part);
        }

        var finished = Assert.IsType<AssistantCompletionFinished>(parts.Last());
        Assert.Equal(string.Empty, finished.Completion.AssistantMessage.Content);

        var toolCall = Assert.Single(finished.Completion.ToolCalls);
        Assert.Equal("call_get-current-time", toolCall.Id);
        Assert.Equal("get-current-time", toolCall.Name);
        Assert.Equal("""{"timezone":"UTC"}""", toolCall.ArgumentsJson);
    }

    private static OpenAiCompatibleAssistantClient CreateClient(
        HttpMessageHandler handler,
        string model,
        string apiKey,
        string? reasoningEffort = null)
    {
        var options = Options.Create(new AssistantOptions
        {
            Provider = "OpenAiCompatible",
            OpenAiCompatible = new OpenAiCompatibleOptions
            {
                BaseUrl = "https://api.openai.com/v1",
                Model = model,
                ApiKey = apiKey,
                ReasoningEffort = reasoningEffort
            }
        });

        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.openai.com/v1/")
        };
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        return new OpenAiCompatibleAssistantClient(httpClient, options);
    }

    private static HttpResponseMessage CreateAssistantContentResponse(string content)
    {
        var json = $$"""
            {
              "choices": [
                {
                  "message": {
                    "role": "assistant",
                    "content": {{JsonSerializer.Serialize(content)}}
                  }
                }
              ]
            }
            """;

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json)
        };
    }

    private static HttpResponseMessage CreateToolCallResponse(string id, string name, string argumentsJson)
    {
        var json = $$"""
            {
              "choices": [
                {
                  "message": {
                    "role": "assistant",
                    "content": null,
                    "tool_calls": [
                      {
                        "id": {{JsonSerializer.Serialize(id)}},
                        "type": "function",
                        "function": {
                          "name": {{JsonSerializer.Serialize(name)}},
                          "arguments": {{JsonSerializer.Serialize(argumentsJson)}}
                        }
                      }
                    ]
                  }
                }
              ]
            }
            """;

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json)
        };
    }

    private static HttpResponseMessage CreateStreamingContentResponse(params string[] contentChunks)
    {
        var builder = new System.Text.StringBuilder();
        foreach (var chunk in contentChunks)
        {
            builder.Append("data: ");
            builder.Append(JsonSerializer.Serialize(new
            {
                choices = new[]
                {
                    new
                    {
                        delta = new { content = chunk }
                    }
                }
            }));
            builder.Append('\n');
            builder.Append('\n');
        }

        builder.Append("data: [DONE]\n\n");

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(builder.ToString(), System.Text.Encoding.UTF8, "text/event-stream")
        };
    }

    private static HttpResponseMessage CreateStreamingToolCallResponse(string id, string name, string argumentsJson)
    {
        var idChunk = JsonSerializer.Serialize(new
        {
            choices = new[]
            {
                new
                {
                    delta = new
                    {
                        tool_calls = new[]
                        {
                            new
                            {
                                index = 0,
                                id,
                                function = new { name = (string?)null, arguments = (string?)null }
                            }
                        }
                    }
                }
            }
        });

        var nameChunk = JsonSerializer.Serialize(new
        {
            choices = new[]
            {
                new
                {
                    delta = new
                    {
                        tool_calls = new[]
                        {
                            new
                            {
                                index = 0,
                                function = new { name, arguments = (string?)null }
                            }
                        }
                    }
                }
            }
        });

        var argumentsChunk = JsonSerializer.Serialize(new
        {
            choices = new[]
            {
                new
                {
                    delta = new
                    {
                        tool_calls = new[]
                        {
                            new
                            {
                                index = 0,
                                function = new { name = (string?)null, arguments = argumentsJson }
                            }
                        }
                    }
                }
            }
        });

        var body = $"""
            data: {idChunk}


            data: {nameChunk}


            data: {argumentsChunk}


            data: [DONE]


            """;

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(body, System.Text.Encoding.UTF8, "text/event-stream")
        };
    }

    private sealed class RecordingHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responseFactory;

        public RecordingHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
        {
            _responseFactory = responseFactory;
        }

        public HttpRequestMessage? LastRequest { get; private set; }

        public string? LastRequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastRequest = request;
            LastRequestBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);

            return _responseFactory(request);
        }
    }
}

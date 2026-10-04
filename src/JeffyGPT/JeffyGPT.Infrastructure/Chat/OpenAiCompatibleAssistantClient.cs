using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using JeffyGPT.Application.Chat;
using JeffyGPT.Contracts.Chat;
using JeffyGPT.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace JeffyGPT.Infrastructure.Chat;

public class OpenAiCompatibleAssistantClient : IAssistantClient
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _httpClient;
    private readonly OpenAiCompatibleOptions _options;

    public OpenAiCompatibleAssistantClient(HttpClient httpClient, IOptions<AssistantOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value.OpenAiCompatible;
    }

    public async Task<AssistantCompletion> CompleteAsync(
        IReadOnlyList<ChatMessageDto> messages,
        IReadOnlyList<ToolDefinitionDto> tools,
        CancellationToken cancellationToken = default)
    {
        var requestBody = BuildRequest(messages, tools, stream: false);

        using var response = await _httpClient.PostAsJsonAsync(
            "chat/completions",
            requestBody,
            SerializerOptions,
            cancellationToken);

        EnsureSuccessStatusCode(response);

        var completionResponse = await response.Content.ReadFromJsonAsync<ChatCompletionResponse>(
            SerializerOptions,
            cancellationToken);

        if (!TryGetFirstChoice(completionResponse?.Choices, out var firstChoice))
        {
            throw new AssistantClientException("OpenAI-compatible chat completion response did not include any choices.");
        }

        var message = firstChoice.Message
            ?? throw new AssistantClientException("OpenAI-compatible chat completion response did not include an assistant message.");

        var assistantMessage = new ChatMessageDto("assistant", message.Content ?? string.Empty);

        var toolCalls = (message.ToolCalls ?? [])
            .Select(toolCall => new ToolCallDto(
                toolCall.Id,
                toolCall.Function.Name,
                toolCall.Function.Arguments ?? "{}"))
            .ToList();

        return new AssistantCompletion(assistantMessage, toolCalls);
    }

    public async IAsyncEnumerable<AssistantStreamPart> CompleteStreamingAsync(
        IReadOnlyList<ChatMessageDto> messages,
        IReadOnlyList<ToolDefinitionDto> tools,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var requestBody = BuildRequest(messages, tools, stream: true);

        using var response = await _httpClient.PostAsJsonAsync(
            "chat/completions",
            requestBody,
            SerializerOptions,
            cancellationToken);

        EnsureSuccessStatusCode(response);

        await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(responseStream);

        var contentBuilder = new StringBuilder();
        var toolCallBuilders = new Dictionary<int, StreamingToolCallBuilder>();

        await foreach (var chunk in ReadChunksAsync(reader, cancellationToken))
        {
            if (!TryGetFirstChoice(chunk.Choices, out var choice) || choice.Delta is null)
            {
                continue;
            }

            var delta = choice.Delta;

            if (!string.IsNullOrEmpty(delta.Content))
            {
                contentBuilder.Append(delta.Content);
                yield return new AssistantContentDelta(delta.Content);
            }

            foreach (var toolCallDelta in delta.ToolCalls ?? [])
            {
                if (!toolCallBuilders.TryGetValue(toolCallDelta.Index, out var builder))
                {
                    builder = new StreamingToolCallBuilder();
                    toolCallBuilders[toolCallDelta.Index] = builder;
                }

                builder.ApplyDelta(toolCallDelta);
            }
        }

        var toolCalls = BuildStreamedToolCalls(toolCallBuilders);
        var assistantMessage = new ChatMessageDto("assistant", contentBuilder.ToString(), toolCalls.Count > 0 ? toolCalls : null);
        yield return new AssistantCompletionFinished(new AssistantCompletion(assistantMessage, toolCalls));
    }

    private ChatCompletionRequest BuildRequest(
        IReadOnlyList<ChatMessageDto> messages,
        IReadOnlyList<ToolDefinitionDto> tools,
        bool stream)
    {
        return new ChatCompletionRequest
        {
            Model = _options.Model,
            Messages = messages.Select(MapOutboundMessage).ToList(),
            Tools = tools.Select(MapTool).ToList(),
            ToolChoice = "auto",
            ReasoningEffort = NormalizeReasoningEffort(_options.ReasoningEffort),
            Stream = stream
        };
    }

    private static ChatCompletionTool MapTool(ToolDefinitionDto tool) =>
        new()
        {
            Type = "function",
            Function = new ChatCompletionFunction
            {
                Name = tool.Name,
                Description = tool.Description,
                Parameters = JsonSerializer.Deserialize<JsonElement>(tool.ParametersJsonSchema)
            }
        };

    internal static string? NormalizeReasoningEffort(string? reasoningEffort) =>
        string.IsNullOrWhiteSpace(reasoningEffort) ? null : reasoningEffort.Trim();

    /// <summary>
    /// Reads SSE data lines and yields each parsed chunk until the provider sends [DONE]
    /// or the stream ends. Malformed lines are skipped.
    /// </summary>
    private static async IAsyncEnumerable<ChatCompletionStreamChunk> ReadChunksAsync(
        StreamReader reader,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            var sseLine = TryParseSseDataLine(line);
            if (sseLine.Kind == SseLineKind.Done)
            {
                yield break;
            }

            if (sseLine.Kind != SseLineKind.Data)
            {
                continue;
            }

            ChatCompletionStreamChunk? chunk;
            try
            {
                chunk = JsonSerializer.Deserialize<ChatCompletionStreamChunk>(sseLine.Data!, SerializerOptions);
            }
            catch (JsonException)
            {
                continue;
            }

            if (chunk is not null)
            {
                yield return chunk;
            }
        }
    }

    private static List<ToolCallDto> BuildStreamedToolCalls(
        Dictionary<int, StreamingToolCallBuilder> toolCallBuilders)
    {
        return toolCallBuilders
            .OrderBy(pair => pair.Key)
            .Select(pair =>
            {
                try
                {
                    return pair.Value.ToToolCallDto();
                }
                catch (InvalidOperationException ex)
                {
                    throw new AssistantClientException(
                        "Streaming tool call was incomplete when the provider finished the stream.",
                        ex);
                }
            })
            .ToList();
    }

    private sealed class StreamingToolCallBuilder
    {
        private string? _id;
        private string? _name;
        private readonly StringBuilder _arguments = new();

        public void ApplyDelta(ChatCompletionStreamToolCall delta)
        {
            if (!string.IsNullOrEmpty(delta.Id))
            {
                _id = delta.Id;
            }

            if (!string.IsNullOrEmpty(delta.Function?.Name))
            {
                _name = delta.Function.Name;
            }

            if (!string.IsNullOrEmpty(delta.Function?.Arguments))
            {
                _arguments.Append(delta.Function.Arguments);
            }
        }

        public ToolCallDto ToToolCallDto()
        {
            return new ToolCallDto(
                _id ?? throw new InvalidOperationException("Streaming tool call did not include an id."),
                _name ?? throw new InvalidOperationException("Streaming tool call did not include a name."),
                _arguments.Length > 0 ? _arguments.ToString() : "{}");
        }
    }

    private static ChatCompletionMessage MapOutboundMessage(ChatMessageDto message)
    {
        var toolCalls = message.ToolCalls?
            .Select(toolCall => new ChatCompletionToolCall
            {
                Id = toolCall.Id,
                Type = "function",
                Function = new ChatCompletionToolCallFunction
                {
                    Name = toolCall.Name,
                    Arguments = toolCall.ArgumentsJson
                }
            })
            .ToList();

        return new ChatCompletionMessage
        {
            Role = message.Role,
            Content = MapOutboundContent(message.Content, toolCalls?.Count ?? 0),
            ToolCalls = toolCalls,
            ToolCallId = message.ToolCallId
        };
    }

    internal static bool ShouldOmitContentForToolCalls(int toolCallCount, string? content) =>
        toolCallCount > 0 && string.IsNullOrEmpty(content);

    internal static string? MapOutboundContent(string? content, int toolCallCount) =>
        ShouldOmitContentForToolCalls(toolCallCount, content) ? null : content;

    internal readonly record struct SseDataLine(SseLineKind Kind, string? Data = null);

    internal static SseDataLine TryParseSseDataLine(string line)
    {
        if (line.Length == 0)
        {
            return new SseDataLine(SseLineKind.Empty);
        }

        if (!line.StartsWith("data: ", StringComparison.Ordinal))
        {
            return new SseDataLine(SseLineKind.NonData);
        }

        var data = line["data: ".Length..].Trim();
        if (data == "[DONE]")
        {
            return new SseDataLine(SseLineKind.Done);
        }

        return new SseDataLine(SseLineKind.Data, data);
    }

    internal static bool TryGetFirstChoice<T>(IReadOnlyList<T>? choices, out T choice)
    {
        if (choices is { Count: > 0 })
        {
            choice = choices[0];
            return true;
        }

        choice = default!;
        return false;
    }

    internal static void ConfigureHttpClient(HttpClient httpClient, OpenAiCompatibleOptions options, string apiKey)
    {
        var baseUrl = options.BaseUrl.TrimEnd('/') + "/";
        httpClient.BaseAddress = new Uri(baseUrl);
        httpClient.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);

        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        }
    }

    /// <summary>
    /// Throws with the status code only — provider error bodies stay out of the message
    /// because AssistantClientException messages are forwarded to the frontend.
    /// </summary>
    private static void EnsureSuccessStatusCode(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var statusCode = (int)response.StatusCode;
        var reasonPhrase = response.ReasonPhrase ?? response.StatusCode.ToString();
        throw new AssistantClientException($"Assistant request failed ({statusCode} {reasonPhrase}).");
    }
}

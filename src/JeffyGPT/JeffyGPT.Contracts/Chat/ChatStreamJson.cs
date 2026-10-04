using System.Text.Json;
using System.Text.Json.Serialization;

namespace JeffyGPT.Contracts.Chat;

/// <summary>
/// Shared JSON options for NDJSON chat stream events. Used by the API endpoint and
/// serialization tests so wire shape cannot drift between production and tests.
/// </summary>
public static class ChatStreamJson
{
    public static JsonSerializerOptions WireOptions { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
}

using System.Text.Json;
using System.Text.Json.Nodes;

namespace JeffyGPT.Infrastructure.Mcp;

internal static class McpJsonHelpers
{
    public static string SerializeInputSchema(JsonElement schema)
    {
        if (schema.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            return """{"type":"object","properties":{},"additionalProperties":false}""";
        }

        return schema.GetRawText();
    }

    public static IReadOnlyDictionary<string, object?> ParseArguments(string argumentsJson)
    {
        if (string.IsNullOrWhiteSpace(argumentsJson))
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal);
        }

        using var document = JsonDocument.Parse(argumentsJson);
        return document.RootElement.EnumerateObject()
            .ToDictionary(
                property => property.Name,
                property => ConvertJsonValue(property.Value),
                StringComparer.Ordinal);
    }

    /// <summary>
    /// Content stored on a tool message. Precedence is structured content, then one text block
    /// (returned as-is, so it may not be JSON), then several text blocks as a JSON array, then "{}".
    /// </summary>
    public static string SerializeToolResultContent(ModelContextProtocol.Protocol.CallToolResult result)
    {
        if (result.StructuredContent is not null)
        {
            return result.StructuredContent.Value.GetRawText();
        }

        return FormatTextBlocks(ReadTextBlocks(result));
    }

    private static List<string>? ReadTextBlocks(ModelContextProtocol.Protocol.CallToolResult result) =>
        result.Content?
            .OfType<ModelContextProtocol.Protocol.TextContentBlock>()
            .Select(block => block.Text)
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .ToList();

    private static string FormatTextBlocks(List<string>? textBlocks)
    {
        if (textBlocks is { Count: 1 })
        {
            return textBlocks[0];
        }

        if (textBlocks is { Count: > 1 })
        {
            return JsonSerializer.Serialize(textBlocks);
        }

        return JsonSerializer.Serialize(new { });
    }

    private static object? ConvertJsonValue(JsonElement element) =>
        element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number when element.TryGetInt64(out var longValue) => longValue,
            JsonValueKind.Number => element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            JsonValueKind.Object => JsonNode.Parse(element.GetRawText()),
            JsonValueKind.Array => JsonNode.Parse(element.GetRawText()),
            _ => element.GetRawText()
        };
}

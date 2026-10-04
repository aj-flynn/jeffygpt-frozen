using System.Text.Json;
using System.Text.Json.Nodes;
using JeffyGPT.Infrastructure.Mcp;

namespace JeffyGPT.Application.Tests;

public class McpJsonHelpersTests
{
    private const string ClosedEmptyObject =
        """{"type":"object","properties":{},"additionalProperties":false}""";

    [Fact]
    public void SerializeInputSchema_MissingSchema_UsesAClosedEmptyObject()
    {
        Assert.Equal(ClosedEmptyObject, McpJsonHelpers.SerializeInputSchema(default));

        using var document = JsonDocument.Parse("null");
        Assert.Equal(ClosedEmptyObject, McpJsonHelpers.SerializeInputSchema(document.RootElement));
    }

    [Fact]
    public void SerializeInputSchema_PresentSchema_ReturnsTheRawText()
    {
        const string schema =
            """{"type":"object","properties":{"path":{"type":"string"}},"additionalProperties":false}""";

        using var document = JsonDocument.Parse(schema);

        Assert.Equal(schema, McpJsonHelpers.SerializeInputSchema(document.RootElement));
    }

    [Fact]
    public void ParseArguments_Blank_ReturnsAnEmptyDictionary()
    {
        Assert.Empty(McpJsonHelpers.ParseArguments(null!));
        Assert.Empty(McpJsonHelpers.ParseArguments(""));
        Assert.Empty(McpJsonHelpers.ParseArguments("   "));
    }

    [Fact]
    public void ParseArguments_JsonValues_ConvertsEachKind()
    {
        var arguments = McpJsonHelpers.ParseArguments(
            """
            {
              "name": "readme",
              "count": 2,
              "ratio": 1.5,
              "ok": true,
              "no": false,
              "empty": null,
              "meta": { "a": 1 },
              "tags": ["a"]
            }
            """);

        // Object and array arms return JsonNode, so the switch's common type is JsonNode.
        // String, number, and bool values convert to JsonNode and are read back as CLR values.
        Assert.Equal("readme", Read<string>(arguments, "name"));
        Assert.Equal(2L, Read<long>(arguments, "count"));
        Assert.Equal(1.5d, Read<double>(arguments, "ratio"));
        Assert.True(Read<bool>(arguments, "ok"));
        Assert.False(Read<bool>(arguments, "no"));
        Assert.Null(arguments["empty"]);

        var meta = Assert.IsType<JsonObject>(arguments["meta"]);
        Assert.Equal(1, meta["a"]!.GetValue<int>());

        var tags = Assert.IsType<JsonArray>(arguments["tags"]);
        Assert.Equal("a", tags[0]!.GetValue<string>());
    }

    private static T Read<T>(IReadOnlyDictionary<string, object?> arguments, string name) =>
        Assert.IsAssignableFrom<JsonNode>(arguments[name]).GetValue<T>();
}

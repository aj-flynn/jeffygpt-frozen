using JeffyGPT.Application.Tests.TestDoubles;
using JeffyGPT.Infrastructure.Tools;
using Json.Schema;

namespace JeffyGPT.Application.Tests.Tools;

public class ToolArgumentValidatorTests
{
    private static readonly JsonSchemaToolArgumentValidator Validator = new();

    private const string ReadFileSchema = """
        {
          "type": "object",
          "required": ["rootAlias", "relativePath"],
          "properties": {
            "rootAlias": { "type": "string", "enum": ["docs"] },
            "relativePath": { "type": "string" }
          },
          "additionalProperties": false
        }
        """;

    private const string ListFilesSchema = """
        {
          "type": "object",
          "required": ["rootAlias", "relativePath"],
          "properties": {
            "rootAlias": { "type": "string", "enum": ["docs"] },
            "relativePath": { "type": "string" }
          },
          "additionalProperties": false
        }
        """;

    [Fact]
    public void Validate_GetCurrentTimeSchema_AcceptsEmptyObject()
    {
        var schema = JsonSchema.FromText(new GetCurrentTimeToolHandler().Definition.ParametersJsonSchema);

        var result = Validator.Validate(schema, "get-current-time", "{}");

        Assert.True(result.IsValid);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void Validate_MalformedJson_ReturnsInvalid()
    {
        var schema = JsonSchema.FromText(new GetCurrentTimeToolHandler().Definition.ParametersJsonSchema);

        var result = Validator.Validate(schema, "get-current-time", "{not json");

        Assert.False(result.IsValid);
        Assert.Equal("Arguments must be valid JSON.", result.ErrorMessage);
    }

    [Fact]
    public void Validate_WhitespaceArguments_ReturnsInvalid()
    {
        var schema = JsonSchema.FromText(new GetCurrentTimeToolHandler().Definition.ParametersJsonSchema);

        var result = Validator.Validate(schema, "get-current-time", "   ");

        Assert.False(result.IsValid);
        Assert.Equal("Arguments must be valid JSON.", result.ErrorMessage);
    }

    [Fact]
    public void Validate_ExtraProperty_ReturnsInvalid()
    {
        var schema = JsonSchema.FromText(new GetCurrentTimeToolHandler().Definition.ParametersJsonSchema);

        var result = Validator.Validate(schema, "get-current-time", """{"foo":1}""");

        Assert.False(result.IsValid);
        Assert.NotNull(result.ErrorMessage);
        Assert.StartsWith("Invalid arguments for get-current-time:", result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_MissingRequiredProperty_ReturnsInvalid()
    {
        const string schemaJson = """
            {
              "type": "object",
              "properties": {
                "query": { "type": "string" }
              },
              "required": ["query"],
              "additionalProperties": false
            }
            """;
        var schema = JsonSchema.FromText(schemaJson);

        var result = Validator.Validate(schema, "echo-query", "{}");

        Assert.False(result.IsValid);
        Assert.NotNull(result.ErrorMessage);
        Assert.StartsWith("Invalid arguments for echo-query:", result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_RequiredPropertyPresent_ReturnsValid()
    {
        const string schemaJson = """
            {
              "type": "object",
              "properties": {
                "query": { "type": "string" }
              },
              "required": ["query"],
              "additionalProperties": false
            }
            """;
        var schema = JsonSchema.FromText(schemaJson);

        var result = Validator.Validate(schema, "echo-query", """{"query":"hello"}""");

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_ReadFileSchema_MissingRootAlias_ReturnsInvalid()
    {
        var schema = JsonSchema.FromText(ReadFileSchema);

        var result = Validator.Validate(schema, "read-file", """{"relativePath":"overview.md"}""");

        Assert.False(result.IsValid);
        Assert.NotNull(result.ErrorMessage);
        Assert.StartsWith("Invalid arguments for read-file:", result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_ReadFileSchema_MissingRelativePath_ReturnsInvalid()
    {
        var schema = JsonSchema.FromText(ReadFileSchema);

        var result = Validator.Validate(schema, "read-file", """{"rootAlias":"docs"}""");

        Assert.False(result.IsValid);
        Assert.NotNull(result.ErrorMessage);
        Assert.StartsWith("Invalid arguments for read-file:", result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_ReadFileSchema_ExtraProperty_ReturnsInvalid()
    {
        var schema = JsonSchema.FromText(ReadFileSchema);

        var result = Validator.Validate(
            schema,
            "read-file",
            """{"rootAlias":"docs","relativePath":"overview.md","extra":true}""");

        Assert.False(result.IsValid);
        Assert.NotNull(result.ErrorMessage);
        Assert.StartsWith("Invalid arguments for read-file:", result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_ReadFileSchema_InvalidRootAlias_ReturnsInvalid()
    {
        var schema = JsonSchema.FromText(ReadFileSchema);

        var result = Validator.Validate(
            schema,
            "read-file",
            """{"rootAlias":"unknown","relativePath":"overview.md"}""");

        Assert.False(result.IsValid);
        Assert.NotNull(result.ErrorMessage);
        Assert.StartsWith("Invalid arguments for read-file:", result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_ListFilesSchema_MissingRootAlias_ReturnsInvalid()
    {
        var schema = JsonSchema.FromText(ListFilesSchema);

        var result = Validator.Validate(schema, "list-files", """{"relativePath":""}""");

        Assert.False(result.IsValid);
        Assert.NotNull(result.ErrorMessage);
        Assert.StartsWith("Invalid arguments for list-files:", result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_ListFilesSchema_ExtraProperty_ReturnsInvalid()
    {
        var schema = JsonSchema.FromText(ListFilesSchema);

        var result = Validator.Validate(
            schema,
            "list-files",
            """{"rootAlias":"docs","relativePath":"","extra":true}""");

        Assert.False(result.IsValid);
        Assert.NotNull(result.ErrorMessage);
        Assert.StartsWith("Invalid arguments for list-files:", result.ErrorMessage, StringComparison.Ordinal);
    }
}

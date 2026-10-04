using Json.Schema;

namespace JeffyGPT.Application.Chat;

public interface IToolArgumentValidator
{
    ToolArgumentValidationResult Validate(JsonSchema schema, string toolName, string argumentsJson);
}

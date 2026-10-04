using System.Text.Json;
using JeffyGPT.Application.Chat;
using Json.Schema;

namespace JeffyGPT.Infrastructure.Tools;

public class JsonSchemaToolArgumentValidator : IToolArgumentValidator
{
    private const string InvalidJsonMessage = "Arguments must be valid JSON.";

    public ToolArgumentValidationResult Validate(JsonSchema schema, string toolName, string argumentsJson)
    {
        if (string.IsNullOrWhiteSpace(argumentsJson))
        {
            return new ToolArgumentValidationResult(false, InvalidJsonMessage);
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(argumentsJson);
        }
        catch (JsonException)
        {
            return new ToolArgumentValidationResult(false, InvalidJsonMessage);
        }

        using (document)
        {
            var evaluation = schema.Evaluate(document.RootElement, new EvaluationOptions
            {
                OutputFormat = OutputFormat.List,
            });

            if (evaluation.IsValid)
            {
                return new ToolArgumentValidationResult(true, null);
            }

            var summary = BuildErrorSummary(evaluation);
            return new ToolArgumentValidationResult(
                false,
                $"Invalid arguments for {toolName}: {summary}");
        }
    }

    private static string BuildErrorSummary(EvaluationResults evaluation)
    {
        var messages = new List<string>();
        CollectErrors(evaluation, messages);

        if (messages.Count == 0)
        {
            return "arguments do not match the tool schema.";
        }

        return string.Join("; ", messages.Distinct(StringComparer.Ordinal));
    }

    private static void CollectErrors(EvaluationResults evaluation, List<string> messages)
    {
        if (evaluation.Errors is not null)
        {
            foreach (var error in evaluation.Errors.Values)
            {
                if (!string.IsNullOrWhiteSpace(error))
                {
                    messages.Add(error);
                }
            }
        }

        if (evaluation.Details is null)
        {
            return;
        }

        foreach (var detail in evaluation.Details)
        {
            CollectErrors(detail, messages);
        }
    }
}

using JeffyGPT.Application.Chat;

namespace JeffyGPT.Infrastructure.Configuration;

internal static class OpenAiApiKeyResolver
{
    public static string Resolve(OpenAiCompatibleOptions options)
    {
        if (!options.RequireApiKey)
        {
            return string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(options.ApiKey))
        {
            return options.ApiKey;
        }

        var environmentVariableName = options.ApiKeyEnvironmentVariable;
        if (!string.IsNullOrWhiteSpace(environmentVariableName))
        {
            var environmentValue = Environment.GetEnvironmentVariable(environmentVariableName);
            if (!string.IsNullOrWhiteSpace(environmentValue))
            {
                return environmentValue;
            }
        }

        throw new AssistantClientException(
            "OpenAI-compatible assistant requires an API key. " +
            $"Set JeffyGPT:Assistant:OpenAiCompatible:ApiKey or the {environmentVariableName} environment variable.");
    }
}

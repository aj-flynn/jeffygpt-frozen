using JeffyGPT.Application.Chat;
using JeffyGPT.Infrastructure.Chat;
using JeffyGPT.Infrastructure.Configuration;
using JeffyGPT.Infrastructure.Mcp;
using JeffyGPT.Infrastructure.Tools;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace JeffyGPT.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        return services.AddInfrastructure(new ConfigurationBuilder().Build());
    }

    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<AssistantOptions>(configuration.GetSection(AssistantOptions.SectionName));
        services.Configure<McpOptions>(configuration.GetSection(McpOptions.SectionName));

        var assistantOptions = new AssistantOptions();
        configuration.GetSection(AssistantOptions.SectionName).Bind(assistantOptions);

        switch (assistantOptions.Provider.ToUpperInvariant())
        {
            case "FAKE":
                services.AddSingleton<IAssistantClient, FakeAssistantClient>();
                break;

            case "OPENAICOMPATIBLE":
                // Resolve API key when the HttpClient is created (first request), not at registration,
                // so Aspire can start the Api/web stack even if OPENAI_API_KEY is missing yet.
                services.AddHttpClient<IAssistantClient, OpenAiCompatibleAssistantClient>((serviceProvider, httpClient) =>
                {
                    var openAiOptions = serviceProvider
                        .GetRequiredService<IOptions<AssistantOptions>>()
                        .Value
                        .OpenAiCompatible;
                    var apiKey = OpenAiApiKeyResolver.Resolve(openAiOptions);
                    OpenAiCompatibleAssistantClient.ConfigureHttpClient(httpClient, openAiOptions, apiKey);
                });
                break;

            default:
                throw new InvalidOperationException(
                    $"Unknown JeffyGPT:Assistant:Provider '{assistantOptions.Provider}'. " +
                    "Supported values: Fake, OpenAiCompatible.");
        }

        services.AddMcpTools();
        return services;
    }
}

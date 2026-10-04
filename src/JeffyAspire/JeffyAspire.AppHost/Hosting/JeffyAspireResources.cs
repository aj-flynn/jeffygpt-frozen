using Aspire.Hosting.JavaScript;

namespace JeffyAspire.Hosting;

internal static class JeffyAspireResources
{
    private const int ViteHttpPort = 5173;
    private const string OpenAiApiKeyParameterName = "openai-api-key";

    // jeffymcp stays non-external (no WithExternalHttpEndpoints) — unauthenticated MCP HTTP + FS tools.
    // Hosted as HTTP under Aspire so the dashboard can show Api → JeffyMCP request traces in demos.
    public static IResourceBuilder<ProjectResource> AddJeffyMcp(this IDistributedApplicationBuilder builder)
    {
        return builder.AddProject<Projects.JeffyMCP>("jeffymcp", launchProfileName: AssistantRunSettings.HttpProfile)
                                                    .WithHttpHealthCheck("/alive");
    }

    public static IResourceBuilder<ProjectResource> AddJeffyApi(
        this IDistributedApplicationBuilder builder,
        IResourceBuilder<ProjectResource> jeffymcp)
    {
        // Assistant provider: AppHost launch profile (http-openai) and/or .run-assistant-provider marker
        // written by VS Code/Cursor preLaunchTask (Aspire debugger type cannot set child env reliably).
        var runSettings = AssistantRunSettings.Resolve(builder.AppHostDirectory);

        var api = builder.AddProject<Projects.JeffyGPT_Api>("api", launchProfileName: runSettings.ApiLaunchProfile)
                                                            .WithReference(jeffymcp)
                                                            .WaitFor(jeffymcp)
                                                            .WithEnvironment("JeffyGPT__Mcp__Servers__0__Transport", "Http")
                                                            .WithEnvironment("JeffyGPT__Mcp__Servers__0__Endpoint", jeffymcp.GetEndpoint("http"))
                                                            .WithHttpHealthCheck("/health");

        if (!string.IsNullOrWhiteSpace(runSettings.AssistantProvider))
        {
            api = api.WithEnvironment("JeffyGPT__Assistant__Provider", runSettings.AssistantProvider);
        }

        api = api.WithOpenAiApiKeyIfPresent(builder);

        Console.WriteLine(
            $"[JeffyAspire] Api launch profile '{runSettings.ApiLaunchProfile}', assistant provider '{runSettings.AssistantProvider ?? "(from Api appsettings)"}'.");

        return api;
    }

    public static IResourceBuilder<ViteAppResource> AddJeffyWeb(
        this IDistributedApplicationBuilder builder,
        IResourceBuilder<ProjectResource> api)
    {
        return builder.AddViteApp("web", "../../JeffyGPT/JeffyGPT.Web")
            .WithReference(api)
            .WaitFor(api)
            .WithExternalHttpEndpoints()
            .WithEnvironment("NODE_ENV", "development")
            .WithEnvironment("BROWSER", "none")
            .WithEndpoint("http", endpoint =>
            {
                endpoint.IsProxied = false;
                endpoint.Port = ViteHttpPort;
            });
    }

    private static IResourceBuilder<ProjectResource> WithOpenAiApiKeyIfPresent(
        this IResourceBuilder<ProjectResource> api,
        IDistributedApplicationBuilder builder)
    {
        if (!HasOpenAiApiKeyConfigured(builder))
        {
            return api;
        }

        if (string.IsNullOrWhiteSpace(builder.Configuration["Parameters:openai-api-key"]))
        {
            builder.Configuration["Parameters:openai-api-key"] =
                Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        }

        var openAiApiKey = builder.AddParameter(OpenAiApiKeyParameterName, secret: true);
        return api.WithEnvironment("OPENAI_API_KEY", openAiApiKey);
    }

    private static bool HasOpenAiApiKeyConfigured(IDistributedApplicationBuilder builder)
    {
        return !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("OPENAI_API_KEY"))
            || !string.IsNullOrWhiteSpace(builder.Configuration["Parameters:openai-api-key"]);
    }
}

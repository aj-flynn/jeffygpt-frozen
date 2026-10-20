using JeffyGPT.Application.Chat;
using JeffyGPT.Application.Tests.TestDoubles;
using JeffyGPT.Infrastructure;
using JeffyGPT.Infrastructure.Chat;
using JeffyGPT.Infrastructure.Mcp;
using JeffyGPT.Infrastructure.Tools;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace JeffyGPT.Application.Tests;

public class InfrastructureDependencyInjectionTests
{
    [Fact]
    public async Task ToolExecutor_RequiresToolInitializationBeforeResolution()
    {
        var services = new ServiceCollection();
        services.AddInfrastructure();
        services.AddSingleton<IMcpToolBootstrapper, TestMcpToolBootstrapper>();
        using var provider = services.BuildServiceProvider();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            provider.GetRequiredService<IToolExecutor>());
        Assert.Contains("Initialize MCP tools", exception.Message, StringComparison.Ordinal);

        await provider.GetRequiredService<IMcpToolBootstrapper>().InitializeAsync();

        Assert.IsType<ToolExecutor>(provider.GetRequiredService<IToolExecutor>());
    }

    [Fact]
    public void AddInfrastructure_ProviderFake_ResolvesMcpToolServices()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["JeffyGPT:Assistant:Provider"] = "Fake"
        });

        var services = new ServiceCollection();
        services.AddInfrastructure(configuration);
        services.AddSingleton<IMcpToolBootstrapper, TestMcpToolBootstrapper>();

        var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IMcpToolBootstrapper>().InitializeAsync().GetAwaiter().GetResult();

        Assert.IsType<ToolExecutor>(provider.GetRequiredService<IToolExecutor>());
        Assert.IsType<JsonSchemaToolArgumentValidator>(provider.GetRequiredService<IToolArgumentValidator>());
        Assert.IsType<McpToolCatalog>(provider.GetRequiredService<IToolCatalog>());

        var tools = provider.GetRequiredService<IToolCatalog>().GetTools();
        Assert.Contains(tools, tool => tool.Name == "get-current-time");
        Assert.Contains(tools, tool => tool.Name == EchoQueryToolHandler.ToolName);
    }

    [Fact]
    public void AddInfrastructure_ProviderFake_ResolvesFakeAssistantClient()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["JeffyGPT:Assistant:Provider"] = "Fake"
        });

        var services = new ServiceCollection();
        services.AddInfrastructure(configuration);

        var provider = services.BuildServiceProvider();
        var assistantClient = provider.GetRequiredService<IAssistantClient>();

        Assert.IsType<FakeAssistantClient>(assistantClient);
    }

    [Fact]
    public void AddInfrastructure_ProviderOpenAiCompatible_ResolvesOpenAiCompatibleAssistantClient()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["JeffyGPT:Assistant:Provider"] = "OpenAiCompatible",
            ["JeffyGPT:Assistant:OpenAiCompatible:ApiKey"] = "test-api-key"
        });

        var services = new ServiceCollection();
        services.AddInfrastructure(configuration);

        var provider = services.BuildServiceProvider();
        var assistantClient = provider.GetRequiredService<IAssistantClient>();

        Assert.IsType<OpenAiCompatibleAssistantClient>(assistantClient);
    }

    [Fact]
    public void AddInfrastructure_ProviderOpenAiCompatible_ConfiguresHttpClientBaseAddress()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["JeffyGPT:Assistant:Provider"] = "OpenAiCompatible",
            ["JeffyGPT:Assistant:OpenAiCompatible:ApiKey"] = "test-api-key",
            ["JeffyGPT:Assistant:OpenAiCompatible:BaseUrl"] = "https://api.openai.com/v1"
        });

        var services = new ServiceCollection();
        services.AddInfrastructure(configuration);

        var provider = services.BuildServiceProvider();
        var assistantClient = Assert.IsType<OpenAiCompatibleAssistantClient>(
            provider.GetRequiredService<IAssistantClient>());

        var httpClient = GetHttpClient(assistantClient);

        Assert.Equal(new Uri("https://api.openai.com/v1/"), httpClient.BaseAddress);
        Assert.Equal("Bearer", httpClient.DefaultRequestHeaders.Authorization?.Scheme);
        Assert.Equal("test-api-key", httpClient.DefaultRequestHeaders.Authorization?.Parameter);
    }

    [Fact]
    public void AddInfrastructure_UnknownProvider_ThrowsClearException()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["JeffyGPT:Assistant:Provider"] = "UnknownProvider"
        });

        var services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddInfrastructure(configuration));

        Assert.Contains("Unknown JeffyGPT:Assistant:Provider", exception.Message, StringComparison.Ordinal);
        Assert.Contains("UnknownProvider", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddInfrastructure_OpenAiCompatibleWithoutApiKey_ThrowsWhenResolvingClient()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["JeffyGPT:Assistant:Provider"] = "OpenAiCompatible",
            ["JeffyGPT:Assistant:OpenAiCompatible:ApiKey"] = "",
            ["JeffyGPT:Assistant:OpenAiCompatible:ApiKeyEnvironmentVariable"] = "MISSING_OPENAI_API_KEY_FOR_TESTS"
        });

        var services = new ServiceCollection();
        services.AddInfrastructure(configuration);
        var provider = services.BuildServiceProvider();

        var exception = Record.Exception(() => provider.GetRequiredService<IAssistantClient>());

        Assert.NotNull(exception);
        Assert.Contains("API key", GetExceptionChainMessage(exception), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AddInfrastructure_OpenAiCompatibleRequireApiKeyFalse_DoesNotRequireApiKey()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["JeffyGPT:Assistant:Provider"] = "OpenAiCompatible",
            ["JeffyGPT:Assistant:OpenAiCompatible:RequireApiKey"] = "false"
        });

        var services = new ServiceCollection();
        services.AddInfrastructure(configuration);

        var provider = services.BuildServiceProvider();
        var assistantClient = Assert.IsType<OpenAiCompatibleAssistantClient>(
            provider.GetRequiredService<IAssistantClient>());

        var httpClient = GetHttpClient(assistantClient);

        Assert.Null(httpClient.DefaultRequestHeaders.Authorization);
    }

    private static IConfiguration BuildConfiguration(Dictionary<string, string?> values)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }

    private static HttpClient GetHttpClient(OpenAiCompatibleAssistantClient client)
    {
        var field = typeof(OpenAiCompatibleAssistantClient).GetField(
            "_httpClient",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        return (HttpClient)field!.GetValue(client)!;
    }

    private static string GetExceptionChainMessage(Exception exception)
    {
        var messages = new List<string>();
        for (var current = exception; current is not null; current = current.InnerException)
        {
            messages.Add(current.Message);
        }

        return string.Join(" | ", messages);
    }
}

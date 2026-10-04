using JeffyGPT.Application.Chat;
using JeffyGPT.Application.Chat.Tools;
using JeffyGPT.Infrastructure.Configuration;
using JeffyGPT.Infrastructure.Mcp;
using JeffyGPT.Infrastructure.Tools;
using Microsoft.Extensions.DependencyInjection;

namespace JeffyGPT.Infrastructure.Mcp;

public static class McpDependencyInjection
{
    public static IServiceCollection AddMcpTools(this IServiceCollection services)
    {
        services.AddSingleton<McpConnectionManager>();
        services.AddSingleton<IMcpConnectionManager>(sp => sp.GetRequiredService<McpConnectionManager>());
        services.AddSingleton<McpToolHandlerRegistry>();
        services.AddSingleton<IMcpToolBootstrapper, McpToolBootstrapper>();
        services.AddSingleton<IToolArgumentValidator, JsonSchemaToolArgumentValidator>();
        services.AddSingleton<IToolExecutionPolicy, DefaultToolExecutionPolicy>();
        services.AddSingleton<IToolExecutor, ToolExecutor>();
        services.AddSingleton<IToolCatalog, McpToolCatalog>();
        services.AddSingleton<IEnumerable<IToolHandler>>(sp =>
            sp.GetRequiredService<McpToolHandlerRegistry>().Handlers);
        return services;
    }
}

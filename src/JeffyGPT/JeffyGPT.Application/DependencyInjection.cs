using JeffyGPT.Application.Chat;
using Microsoft.Extensions.DependencyInjection;

namespace JeffyGPT.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton<IChatOrchestrator, ChatOrchestrator>();
        return services;
    }
}

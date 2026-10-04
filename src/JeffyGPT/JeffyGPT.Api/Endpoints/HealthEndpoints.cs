using JeffyGPT.Contracts.Health;

namespace JeffyGPT.Api.Endpoints;

public static class HealthEndpoints
{
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/health", GetHealth);
        return app;
    }

    private static HealthResponse GetHealth() => new("ok");
}

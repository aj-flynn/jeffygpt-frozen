using System.Threading.RateLimiting;
using JeffyGPT.Api.Endpoints;
using JeffyGPT.Application;
using JeffyGPT.Infrastructure;
using JeffyGPT.Infrastructure.Configuration;
using JeffyGPT.Infrastructure.Mcp;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        // Local-only product until auth ships — pin to the Vite origin(s), not every loopback port.
        policy.WithOrigins(
                "http://localhost:5173",
                "http://127.0.0.1:5173")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .WithExposedHeaders(ChatEndpoints.ConversationIdHeader);
    });
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter("chat", limiter =>
    {
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.PermitLimit = 60;
        limiter.QueueLimit = 0;
        limiter.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
    });
});

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

await app.Services.GetRequiredService<IMcpToolBootstrapper>().InitializeAsync();

LogAssistantStartup(app.Services);

app.UseCors();
app.UseRateLimiter();
app.MapDefaultEndpoints(mapReadyHealthEndpoint: false);
app.MapHealthEndpoints();
app.MapChatEndpoints();

app.Run();

static void LogAssistantStartup(IServiceProvider services)
{
    var assistantOptions = services.GetRequiredService<IOptions<AssistantOptions>>().Value;
    var startupLogger = services.GetRequiredService<ILoggerFactory>().CreateLogger("JeffyGPT.Startup");
    startupLogger.LogInformation("Assistant provider: {Provider}", assistantOptions.Provider);

    if (assistantOptions.IsOpenAiCompatibleProvider())
    {
        var openAiOptions = assistantOptions.OpenAiCompatible;
        startupLogger.LogInformation(
            "OpenAI-compatible assistant: BaseUrl={BaseUrl}, Model={Model}, RequireApiKey={RequireApiKey}",
            openAiOptions.BaseUrl,
            openAiOptions.Model,
            openAiOptions.RequireApiKey);
    }
}

public partial class Program;

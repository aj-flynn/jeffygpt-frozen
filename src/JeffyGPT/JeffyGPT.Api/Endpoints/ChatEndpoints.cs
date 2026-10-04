using System.Text.Json;
using JeffyGPT.Application.Chat;
using JeffyGPT.Contracts.Chat;

namespace JeffyGPT.Api.Endpoints;

public static class ChatEndpoints
{
    private const string LoggerCategory = "JeffyGPT.Api.Chat";

    private const string UnexpectedChatError =
        "An unexpected error occurred while processing the chat request.";
    public static IEndpointRouteBuilder MapChatEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/chat", HandleChat).RequireRateLimiting("chat");
        app.MapPost("/api/chat/stream", HandleChatStream).RequireRateLimiting("chat");
        return app;
    }

    private static async Task<IResult> HandleChat(
        ChatRequest request,
        IChatOrchestrator orchestrator,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await orchestrator.SendAsync(request, cancellationToken);
            return Results.Ok(response);
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
        catch (AssistantClientException ex)
        {
            return Results.Json(new { error = ex.Message }, statusCode: StatusCodes.Status502BadGateway);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            loggerFactory.CreateLogger(LoggerCategory).LogError(ex, "Unexpected error in /api/chat.");
            return Results.Json(
                new { error = UnexpectedChatError },
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private static async Task HandleChatStream(
        ChatRequest request,
        IChatOrchestrator orchestrator,
        HttpContext httpContext,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        try
        {
            ChatRequestValidator.Validate(request);
        }
        catch (ArgumentException ex)
        {
            httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            httpContext.Response.ContentType = "application/json";
            await httpContext.Response.WriteAsJsonAsync(new { error = ex.Message }, cancellationToken);
            return;
        }

        httpContext.Response.StatusCode = StatusCodes.Status200OK;
        httpContext.Response.ContentType = "application/x-ndjson";

        try
        {
            await foreach (var streamEvent in orchestrator.StreamAsync(request, cancellationToken))
            {
                await WriteStreamEventAsync(httpContext, streamEvent, cancellationToken);
            }
        }
        catch (AssistantClientException ex)
        {
            await WriteStreamEventAsync(
                httpContext,
                new StreamErrorEvent(ex.Message),
                cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            loggerFactory.CreateLogger(LoggerCategory).LogError(ex, "Unexpected error in /api/chat/stream.");
            await WriteStreamEventAsync(
                httpContext,
                new StreamErrorEvent(UnexpectedChatError),
                cancellationToken);
        }
    }

    private static async Task WriteStreamEventAsync(
        HttpContext httpContext,
        ChatStreamEvent streamEvent,
        CancellationToken cancellationToken)
    {
        await JsonSerializer.SerializeAsync(
            httpContext.Response.Body,
            streamEvent,
            ChatStreamJson.WireOptions,
            cancellationToken);
        await httpContext.Response.Body.WriteAsync("\n"u8.ToArray(), cancellationToken);
        await httpContext.Response.Body.FlushAsync(cancellationToken);
    }
}

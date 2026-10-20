using System.Text.Json;
using JeffyGPT.Application.Chat;
using JeffyGPT.Contracts.Chat;

namespace JeffyGPT.Api.Endpoints;

public static class ChatEndpoints
{
    public const string ConversationIdHeader = "X-Conversation-Id";

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
        ChatTurnRequest request,
        IChatOrchestrator orchestrator,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await orchestrator.SendAsync(
                request.ConversationId,
                request.Content,
                cancellationToken);
            return Results.Ok(response);
        }
        catch (Exception ex) when (TryMapTurnStartStatus(ex, out var statusCode))
        {
            return Results.Json(new { error = ex.Message }, statusCode: statusCode);
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
        ChatTurnRequest request,
        IChatOrchestrator orchestrator,
        HttpContext httpContext,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        ChatTurn turn;
        try
        {
            turn = orchestrator.StartTurn(request.ConversationId, request.Content);
        }
        catch (Exception ex) when (TryMapTurnStartStatus(ex, out var statusCode))
        {
            await WriteJsonErrorAsync(httpContext, statusCode, ex.Message, cancellationToken);
            return;
        }

        httpContext.Response.StatusCode = StatusCodes.Status200OK;
        httpContext.Response.ContentType = "application/x-ndjson";
        httpContext.Response.Headers[ConversationIdHeader] = turn.ConversationId;

        try
        {
            await foreach (var streamEvent in turn.Events.WithCancellation(cancellationToken))
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

    private static bool TryMapTurnStartStatus(Exception exception, out int statusCode)
    {
        switch (exception)
        {
            case ConversationNotFoundException:
                statusCode = StatusCodes.Status404NotFound;
                return true;
            case ConversationBusyException:
                statusCode = StatusCodes.Status409Conflict;
                return true;
            case ArgumentException:
                statusCode = StatusCodes.Status400BadRequest;
                return true;
            default:
                statusCode = 0;
                return false;
        }
    }

    private static async Task WriteJsonErrorAsync(
        HttpContext httpContext,
        int statusCode,
        string message,
        CancellationToken cancellationToken)
    {
        httpContext.Response.StatusCode = statusCode;
        httpContext.Response.ContentType = "application/json";
        await httpContext.Response.WriteAsJsonAsync(new { error = message }, cancellationToken);
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

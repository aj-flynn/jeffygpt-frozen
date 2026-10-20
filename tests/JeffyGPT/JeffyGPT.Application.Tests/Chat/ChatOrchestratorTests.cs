using JeffyGPT.Application;
using JeffyGPT.Application.Chat;
using JeffyGPT.Application.Tests.TestDoubles;
using JeffyGPT.Contracts.Chat;
using JeffyGPT.Infrastructure;
using JeffyGPT.Infrastructure.Mcp;
using JeffyGPT.Infrastructure.Tools;
using Microsoft.Extensions.DependencyInjection;

namespace JeffyGPT.Application.Tests.Chat;

public class ChatOrchestratorTests
{
    private static IChatOrchestrator CreateOrchestrator()
    {
        var services = new ServiceCollection();
        services.AddApplication();
        services.AddInfrastructure();
        services.AddSingleton<IMcpToolBootstrapper, TestMcpToolBootstrapper>();
        var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IMcpToolBootstrapper>().InitializeAsync().GetAwaiter().GetResult();
        return provider.GetRequiredService<IChatOrchestrator>();
    }

    private static readonly IToolHandler[] TestHandlers = [new GetCurrentTimeToolHandler()];

    private static ChatOrchestrator CreateOrchestratorWithStub(StubAssistantClient stub) =>
        new(
            stub,
            new McpToolCatalog(CreateRegistry(TestHandlers)),
            new ToolExecutor(
                TestHandlers,
                new JsonSchemaToolArgumentValidator(),
                new DefaultToolExecutionPolicy()));

    private static McpToolHandlerRegistry CreateRegistry(IEnumerable<IToolHandler> handlers)
    {
        var registry = new McpToolHandlerRegistry();
        registry.Register(handlers.ToList());
        return registry;
    }

    private static AssistantCompletion ToolCallCompletion(string callId = "call_stub", string toolName = "get-current-time") =>
        new(
            new ChatMessageDto("assistant", string.Empty, [new ToolCallDto(callId, toolName, "{}")]),
            [new ToolCallDto(callId, toolName, "{}")]);

    private static AssistantCompletion FinalAnswerCompletion(string content = "Final answer.") =>
        new(new ChatMessageDto("assistant", content), []);

    [Fact]
    public async Task SendAsync_WithoutId_ReturnsEchoAndANewId()
    {
        var orchestrator = CreateOrchestrator();

        var first = await orchestrator.SendAsync(null, "  hello there  ");
        var second = await orchestrator.SendAsync(null, "hello there");

        Assert.Equal("Echo: hello there", first.AssistantMessage.Content);
        Assert.NotEqual(first.ConversationId, second.ConversationId);
        Assert.NotEqual(Guid.Empty, Guid.ParseExact(first.ConversationId, "D"));
    }

    [Fact]
    public async Task SendAsync_UnknownOrMalformedId_ThrowsNotFound()
    {
        var orchestrator = CreateOrchestrator();

        var unknown = await Assert.ThrowsAsync<ConversationNotFoundException>(() =>
            orchestrator.SendAsync(Guid.NewGuid().ToString("D"), "hello"));
        var malformed = await Assert.ThrowsAsync<ConversationNotFoundException>(() =>
            orchestrator.SendAsync("not-a-guid", "hello"));

        Assert.Equal(ConversationNotFoundException.ErrorText, unknown.Message);
        Assert.Equal(ConversationNotFoundException.ErrorText, malformed.Message);
    }

    [Fact]
    public async Task SendAsync_BlankContent_Throws()
    {
        var orchestrator = CreateOrchestrator();

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => orchestrator.SendAsync(null, "  "));

        Assert.Equal("A user message is required.", ex.Message);
    }

    [Fact]
    public async Task StartTurn_WhileReading_ThrowsBusy_ThenReleaseAllowsTheNextTurn()
    {
        var orchestrator = CreateOrchestrator();
        var started = await orchestrator.SendAsync(null, "hello");
        var open = orchestrator.StartTurn(started.ConversationId, "still open");
        await using var enumerator = open.Events.GetAsyncEnumerator();
        Assert.True(await enumerator.MoveNextAsync());

        var busy = await Assert.ThrowsAsync<ConversationBusyException>(() =>
            orchestrator.SendAsync(started.ConversationId, "nope"));
        Assert.Equal(ConversationBusyException.ErrorText, busy.Message);

        await enumerator.DisposeAsync();

        var next = await orchestrator.SendAsync(started.ConversationId, "hello");
        Assert.Equal("Echo: hello", next.AssistantMessage.Content);
    }

    [Fact]
    public async Task SendAsync_MessageContainingTime_ReturnsToolCallAndToolResult()
    {
        var orchestrator = CreateOrchestrator();

        var response = await orchestrator.SendAsync(null, "what time is it");

        Assert.Equal("assistant", response.AssistantMessage.Role);
        Assert.StartsWith("The current local time is ", response.AssistantMessage.Content);
        var toolCall = Assert.Single(response.ToolCalls);
        Assert.Equal("get-current-time", toolCall.Name);
        var toolResult = Assert.Single(response.ToolResults);
        Assert.Equal(toolCall.Id, toolResult.ToolCallId);
        Assert.False(toolResult.IsError);
    }

    [Fact]
    public async Task ReadEvents_NormalUserMessage_YieldsDeltasAssistantMessageAndDone()
    {
        var orchestrator = CreateOrchestrator();

        var events = await ReadEventsAsync(orchestrator, "hello there");

        Assert.Equal("Echo: hello there", string.Concat(
            events.OfType<AssistantDeltaEvent>().Select(e => e.Content)));
        var assistantMessage = Assert.Single(events.OfType<AssistantMessageEvent>());
        Assert.Equal("Echo: hello there", assistantMessage.Message.Content);
        Assert.Contains(events, e => e is StreamDoneEvent);
    }

    [Fact]
    public async Task SendAsync_NextTurn_SendsStoredToolRoundsAndFinalAnswer()
    {
        var stub = new StubAssistantClient
        {
            Responses = new Queue<AssistantCompletion>(
            [
                ToolCallCompletion("call_round_1"),
                FinalAnswerCompletion("Done after tools."),
                FinalAnswerCompletion("Second turn.")
            ])
        };
        var orchestrator = CreateOrchestratorWithStub(stub);

        var first = await orchestrator.SendAsync(null, "run tool");
        await orchestrator.SendAsync(first.ConversationId, "and again");

        var thirdCall = stub.Calls[2];
        Assert.Equal("system", thirdCall[0].Role);
        Assert.Contains(thirdCall, message => message.Role == "user" && message.Content == "run tool");
        Assert.Contains(thirdCall, message => message.Role == "tool" && message.ToolCallId == "call_round_1");
        Assert.Contains(thirdCall, message => message.Role == "assistant" && message.Content == "Done after tools.");
        Assert.Contains(thirdCall, message => message.Role == "user" && message.Content == "and again");
        Assert.DoesNotContain(thirdCall.Skip(1), message => message.Role == "system");
    }

    [Fact]
    public async Task CancelBeforeAnAnswer_DropsTheUserMessage()
    {
        var stub = new StubAssistantClient
        {
            Responses = new Queue<AssistantCompletion>([FinalAnswerCompletion("kept answer")])
        };
        var orchestrator = CreateOrchestratorWithStub(stub);
        var abandoned = orchestrator.StartTurn(null, "abandoned");
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        await using (var enumerator = abandoned.Events.WithCancellation(cancelled.Token).GetAsyncEnumerator())
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await enumerator.MoveNextAsync());
        }

        await orchestrator.SendAsync(abandoned.ConversationId, "kept");

        var keptCall = Assert.Single(stub.Calls);
        Assert.DoesNotContain(keptCall, message => message.Content == "abandoned");
        Assert.Contains(keptCall, message => message.Content == "kept");
    }

    [Fact]
    public async Task SendAsync_MaxIterations_ReturnsSafeAssistantMessage()
    {
        var stub = new StubAssistantClient();
        for (var index = 0; index <= 40; index++)
        {
            stub.Responses.Enqueue(ToolCallCompletion($"call_{index}", "stub_tool"));
        }

        var orchestrator = CreateOrchestratorWithStub(stub);

        var response = await orchestrator.SendAsync(null, "keep calling tools");

        Assert.Equal(41, stub.Calls.Count);
        Assert.Equal(40, response.ToolCalls.Count);
        Assert.Contains("maximum of 40 tool rounds", response.AssistantMessage.Content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SendAsync_PrependsJeffySystemPrompt_OnEveryAssistantCall()
    {
        var stub = new StubAssistantClient
        {
            Responses = new Queue<AssistantCompletion>(
            [
                ToolCallCompletion("call_round_1"),
                FinalAnswerCompletion()
            ])
        };
        var orchestrator = CreateOrchestratorWithStub(stub);

        await orchestrator.SendAsync(null, "run tool");

        Assert.Equal(2, stub.Calls.Count);
        foreach (var call in stub.Calls)
        {
            var system = Assert.Single(call, message =>
                string.Equals(message.Role, "system", StringComparison.OrdinalIgnoreCase));
            Assert.Equal(JeffyPersonality.SystemPrompt, system.Content);
            Assert.Equal("system", call[0].Role);
        }
    }

    [Fact]
    public async Task SendAsync_OverStoredCharacterBound_ThrowsAndLeavesTheConversationFree()
    {
        var stub = new StubAssistantClient();
        var orchestrator = CreateOrchestratorWithStub(stub);
        var chunk = new string('a', ChatOrchestrator.MaxUserCharacters);
        string? id = null;

        for (var turn = 0; turn < 3; turn++)
        {
            stub.Responses.Enqueue(FinalAnswerCompletion("x"));
            var response = await orchestrator.SendAsync(id, chunk);
            id = response.ConversationId;
        }

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => orchestrator.SendAsync(id, chunk));
        Assert.Contains("128000", ex.Message, StringComparison.Ordinal);

        stub.Responses.Enqueue(FinalAnswerCompletion("still open"));
        var after = await orchestrator.SendAsync(id, "short");
        Assert.Equal("still open", after.AssistantMessage.Content);
    }

    private static async Task<List<ChatStreamEvent>> ReadEventsAsync(
        IChatOrchestrator orchestrator,
        string content)
    {
        var turn = orchestrator.StartTurn(null, content);
        var events = new List<ChatStreamEvent>();
        await foreach (var streamEvent in turn.Events)
        {
            events.Add(streamEvent);
        }

        return events;
    }
}

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
    public async Task SendAsync_EmptyMessages_ThrowsArgumentException()
    {
        var orchestrator = CreateOrchestrator();
        var request = new ChatRequest([]);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            orchestrator.SendAsync(request, CancellationToken.None));
    }

    [Fact]
    public async Task SendAsync_NormalUserMessage_ReturnsEchoResponse()
    {
        var orchestrator = CreateOrchestrator();
        var request = new ChatRequest(
        [
            new ChatMessageDto("user", "hello there")
        ]);

        var response = await orchestrator.SendAsync(request, CancellationToken.None);

        Assert.Equal("assistant", response.AssistantMessage.Role);
        Assert.Equal("Echo: hello there", response.AssistantMessage.Content);
        Assert.Empty(response.ToolCalls);
        Assert.Empty(response.ToolResults);
    }

    [Fact]
    public async Task SendAsync_MessageContainingTime_ReturnsToolCallAndToolResult()
    {
        var orchestrator = CreateOrchestrator();
        var request = new ChatRequest(
        [
            new ChatMessageDto("user", "what time is it")
        ]);

        var response = await orchestrator.SendAsync(request, CancellationToken.None);

        Assert.Equal("assistant", response.AssistantMessage.Role);
        Assert.StartsWith("The current local time is ", response.AssistantMessage.Content);
        Assert.EndsWith(".", response.AssistantMessage.Content);

        var toolCall = Assert.Single(response.ToolCalls);
        Assert.Equal("get-current-time", toolCall.Name);
        Assert.Equal("{}", toolCall.ArgumentsJson);

        var toolResult = Assert.Single(response.ToolResults);
        Assert.Equal(toolCall.Id, toolResult.ToolCallId);
        Assert.False(toolResult.IsError);
        Assert.Contains("local", toolResult.ContentJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task StreamAsync_NormalUserMessage_YieldsDeltasAssistantMessageAndDone()
    {
        var orchestrator = CreateOrchestrator();
        var request = new ChatRequest(
        [
            new ChatMessageDto("user", "hello there")
        ]);

        var events = await CollectStreamEventsAsync(orchestrator, request);

        Assert.Contains(events, e => e is AssistantDeltaEvent { Content.Length: > 0 });
        Assert.Equal("Echo: hello there", string.Concat(
            events.OfType<AssistantDeltaEvent>().Select(e => e.Content)));

        var assistantMessage = Assert.Single(events.OfType<AssistantMessageEvent>());
        Assert.Equal("Echo: hello there", assistantMessage.Message.Content);

        Assert.Contains(events, e => e is StreamDoneEvent);
    }

    [Fact]
    public async Task StreamAsync_MessageContainingTime_YieldsToolEventsThenFinalMessage()
    {
        var orchestrator = CreateOrchestrator();
        var request = new ChatRequest(
        [
            new ChatMessageDto("user", "what time is it")
        ]);

        var events = await CollectStreamEventsAsync(orchestrator, request);

        var toolRound = Assert.Single(events.OfType<AssistantToolRoundEvent>());
        var toolCall = Assert.Single(toolRound.Message.ToolCalls!);
        Assert.Equal("get-current-time", toolCall.Name);

        var toolStartedIndex = events.FindIndex(e => e is ToolCallStartedEvent);
        var toolRoundIndex = events.FindIndex(e => e is AssistantToolRoundEvent);
        Assert.True(toolRoundIndex < toolStartedIndex);

        var toolStarted = Assert.Single(events.OfType<ToolCallStartedEvent>());
        Assert.Equal("get-current-time", toolStarted.ToolCall.Name);

        var toolFinished = Assert.Single(events.OfType<ToolCallFinishedEvent>());
        Assert.Equal(toolStarted.ToolCall.Id, toolFinished.ToolResult.ToolCallId);
        Assert.False(toolFinished.ToolResult.IsError);

        var assistantMessage = Assert.Single(events.OfType<AssistantMessageEvent>());
        Assert.StartsWith("The current local time is ", assistantMessage.Message.Content);
        Assert.Contains(events, e => e is StreamDoneEvent);
    }

    [Fact]
    public async Task SendAsync_AfterToolExecution_SecondAssistantCallReceivesToolHistory()
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
        var request = new ChatRequest([new ChatMessageDto("user", "run tool")]);

        await orchestrator.SendAsync(request, CancellationToken.None);

        Assert.Equal(2, stub.Calls.Count);

        var secondCallMessages = stub.Calls[1];
        var assistantWithTools = Assert.Single(
            secondCallMessages,
            message => string.Equals(message.Role, "assistant", StringComparison.OrdinalIgnoreCase)
                && message.ToolCalls is { Count: > 0 });
        var toolCall = Assert.Single(assistantWithTools.ToolCalls!);
        Assert.Equal("call_round_1", toolCall.Id);

        var toolMessage = Assert.Single(
            secondCallMessages,
            message => string.Equals(message.Role, "tool", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("call_round_1", toolMessage.ToolCallId);
    }

    [Fact]
    public async Task SendAsync_TwoRoundStub_StopsWhenAssistantReturnsNoToolCalls()
    {
        var stub = new StubAssistantClient
        {
            Responses = new Queue<AssistantCompletion>(
            [
                ToolCallCompletion(),
                FinalAnswerCompletion("Done after one tool round.")
            ])
        };
        var orchestrator = CreateOrchestratorWithStub(stub);
        var request = new ChatRequest([new ChatMessageDto("user", "run tool")]);

        var response = await orchestrator.SendAsync(request, CancellationToken.None);

        Assert.Equal(2, stub.Calls.Count);
        Assert.Equal("Done after one tool round.", response.AssistantMessage.Content);
        Assert.Single(response.ToolCalls);
        Assert.Single(response.ToolResults);
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
        var request = new ChatRequest([new ChatMessageDto("user", "keep calling tools")]);

        var response = await orchestrator.SendAsync(request, CancellationToken.None);

        Assert.Equal(41, stub.Calls.Count);
        Assert.Equal(40, response.ToolCalls.Count);
        Assert.Equal(40, response.ToolResults.Count);
        Assert.Contains("maximum of 40 tool rounds", response.AssistantMessage.Content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task StreamAsync_MaxIterations_YieldsSafeAssistantMessageAndDone()
    {
        var stub = new StubAssistantClient();
        for (var index = 0; index <= 40; index++)
        {
            stub.Responses.Enqueue(ToolCallCompletion($"call_{index}", "stub_tool"));
        }

        var orchestrator = CreateOrchestratorWithStub(stub);
        var request = new ChatRequest([new ChatMessageDto("user", "keep calling tools")]);

        var events = await CollectStreamEventsAsync(orchestrator, request);

        Assert.DoesNotContain(events, e => e is StreamErrorEvent);
        Assert.Equal(40, events.Count(e => e is AssistantToolRoundEvent));
        Assert.Equal(40, events.Count(e => e is ToolCallStartedEvent));
        Assert.Equal(40, events.Count(e => e is ToolCallFinishedEvent));

        for (var index = 0; index < events.Count; index++)
        {
            if (events[index] is not ToolCallStartedEvent)
            {
                continue;
            }

            Assert.True(index > 0);
            Assert.IsType<AssistantToolRoundEvent>(events[index - 1]);
        }

        var assistantMessage = Assert.Single(events.OfType<AssistantMessageEvent>());
        Assert.Contains("maximum of 40 tool rounds", assistantMessage.Message.Content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(events, e => e is StreamDoneEvent);
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
        var request = new ChatRequest([new ChatMessageDto("user", "run tool")]);

        await orchestrator.SendAsync(request, CancellationToken.None);

        Assert.Equal(2, stub.Calls.Count);
        foreach (var call in stub.Calls)
        {
            var system = Assert.Single(call, message =>
                string.Equals(message.Role, "system", StringComparison.OrdinalIgnoreCase));
            Assert.Equal(JeffyPersonality.SystemPrompt, system.Content);
            Assert.Equal("system", call[0].Role);
        }
    }

    private static async Task<List<ChatStreamEvent>> CollectStreamEventsAsync(
        IChatOrchestrator orchestrator,
        ChatRequest request)
    {
        var events = new List<ChatStreamEvent>();
        await foreach (var streamEvent in orchestrator.StreamAsync(request, CancellationToken.None))
        {
            events.Add(streamEvent);
        }

        return events;
    }
}

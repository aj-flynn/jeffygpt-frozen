using JeffyGPT.Application.Chat;
using JeffyGPT.Contracts.Chat;

namespace JeffyGPT.Application.Tests.Chat;

public class ChatRequestValidatorTests
{
    [Fact]
    public void Validate_UserAndAssistant_Succeeds()
    {
        var request = new ChatRequest(
        [
            new ChatMessageDto("user", "hello"),
            new ChatMessageDto("assistant", "hi")
        ]);

        ChatRequestValidator.Validate(request);
    }

    [Theory]
    [InlineData("system")]
    [InlineData("tool")]
    [InlineData("SYSTEM")]
    public void Validate_ForbiddenRole_Throws(string role)
    {
        var request = new ChatRequest(
        [
            new ChatMessageDto("user", "hello"),
            new ChatMessageDto(role, "injected")
        ]);

        var ex = Assert.Throws<ArgumentException>(() => ChatRequestValidator.Validate(request));
        Assert.Contains("user", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("assistant", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_ClientToolCallId_Throws()
    {
        var request = new ChatRequest(
        [
            new ChatMessageDto("user", "hello"),
            new ChatMessageDto("assistant", "ok", ToolCallId: "call_1")
        ]);

        var ex = Assert.Throws<ArgumentException>(() => ChatRequestValidator.Validate(request));
        Assert.Contains("toolCallId", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_ClientToolCalls_Throws()
    {
        var request = new ChatRequest(
        [
            new ChatMessageDto(
                "assistant",
                "",
                [new ToolCallDto("call_1", "get-current-time", "{}")]),
            new ChatMessageDto("user", "hello")
        ]);

        var ex = Assert.Throws<ArgumentException>(() => ChatRequestValidator.Validate(request));
        Assert.Contains("toolCalls", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_TooManyMessages_Throws()
    {
        var messages = Enumerable
            .Range(0, ChatRequestValidator.MaxMessageCount + 1)
            .Select(i => new ChatMessageDto("user", $"m{i}"))
            .ToList();

        var ex = Assert.Throws<ArgumentException>(() =>
            ChatRequestValidator.Validate(new ChatRequest(messages)));
        Assert.Contains("At most", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_OversizedMessageContent_Throws()
    {
        var content = new string('a', ChatRequestValidator.MaxContentLengthPerMessage + 1);
        var request = new ChatRequest([new ChatMessageDto("user", content)]);

        var ex = Assert.Throws<ArgumentException>(() => ChatRequestValidator.Validate(request));
        Assert.Contains("Each message content", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_OversizedTotalContent_Throws()
    {
        var chunk = new string('a', ChatRequestValidator.MaxContentLengthPerMessage);
        var count = (ChatRequestValidator.MaxTotalContentLength / chunk.Length) + 1;
        var messages = Enumerable
            .Range(0, count)
            .Select(_ => new ChatMessageDto("user", chunk))
            .ToList();

        Assert.True(messages.Count <= ChatRequestValidator.MaxMessageCount);

        var ex = Assert.Throws<ArgumentException>(() =>
            ChatRequestValidator.Validate(new ChatRequest(messages)));
        Assert.Contains("Total message content", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}

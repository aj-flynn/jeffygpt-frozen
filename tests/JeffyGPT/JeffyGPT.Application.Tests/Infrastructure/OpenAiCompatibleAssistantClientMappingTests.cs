using JeffyGPT.Infrastructure.Chat;

namespace JeffyGPT.Application.Tests;

public class OpenAiCompatibleAssistantClientMappingTests
{
    [Fact]
    public void ShouldOmitContentForToolCalls_WithToolCallsAndEmptyContent_ReturnsTrue()
    {
        Assert.True(OpenAiCompatibleAssistantClient.ShouldOmitContentForToolCalls(1, string.Empty));
        Assert.True(OpenAiCompatibleAssistantClient.ShouldOmitContentForToolCalls(1, null));
    }

    [Fact]
    public void ShouldOmitContentForToolCalls_WithToolCallsAndContent_ReturnsFalse()
    {
        Assert.False(OpenAiCompatibleAssistantClient.ShouldOmitContentForToolCalls(1, "visible content"));
    }

    [Fact]
    public void ShouldOmitContentForToolCalls_WithoutToolCalls_ReturnsFalse()
    {
        Assert.False(OpenAiCompatibleAssistantClient.ShouldOmitContentForToolCalls(0, string.Empty));
    }

    [Fact]
    public void MapOutboundContent_OmitsContentWhenToolCallsPresentAndContentEmpty()
    {
        Assert.Null(OpenAiCompatibleAssistantClient.MapOutboundContent(string.Empty, 1));
    }

    [Fact]
    public void MapOutboundContent_PreservesContentWhenPresent()
    {
        Assert.Equal("keep me", OpenAiCompatibleAssistantClient.MapOutboundContent("keep me", 1));
        Assert.Equal("plain text", OpenAiCompatibleAssistantClient.MapOutboundContent("plain text", 0));
    }

    [Theory]
    [InlineData("", SseLineKind.Empty)]
    [InlineData(": keep-alive", SseLineKind.NonData)]
    [InlineData("event: ping", SseLineKind.NonData)]
    public void TryParseSseDataLine_NonDataLines_ReturnExpectedKind(string line, SseLineKind expectedKind)
    {
        var result = OpenAiCompatibleAssistantClient.TryParseSseDataLine(line);

        Assert.Equal(expectedKind, result.Kind);
        Assert.Null(result.Data);
    }

    [Fact]
    public void TryParseSseDataLine_DoneLine_ReturnsDone()
    {
        var result = OpenAiCompatibleAssistantClient.TryParseSseDataLine("data: [DONE]");

        Assert.Equal(SseLineKind.Done, result.Kind);
        Assert.Null(result.Data);
    }

    [Fact]
    public void TryParseSseDataLine_DataLine_ReturnsPayload()
    {
        var result = OpenAiCompatibleAssistantClient.TryParseSseDataLine("data: {\"choices\":[]}");

        Assert.Equal(SseLineKind.Data, result.Kind);
        Assert.Equal("{\"choices\":[]}", result.Data);
    }

    [Fact]
    public void TryGetFirstChoice_WithChoices_ReturnsFirst()
    {
        Assert.True(OpenAiCompatibleAssistantClient.TryGetFirstChoice(["a", "b"], out var choice));
        Assert.Equal("a", choice);
    }

    [Fact]
    public void TryGetFirstChoice_WithoutChoices_ReturnsFalse()
    {
        Assert.False(OpenAiCompatibleAssistantClient.TryGetFirstChoice<string>(null, out _));
        Assert.False(OpenAiCompatibleAssistantClient.TryGetFirstChoice(Array.Empty<string>(), out _));
    }
}

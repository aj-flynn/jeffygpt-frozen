using JeffyGPT.Application.Chat;
using JeffyGPT.Contracts.Chat;

namespace JeffyGPT.Application.Tests.Chat;

public class JeffyPersonalityTests
{
    [Fact]
    public void WithSystemPrompt_PutsSystemPromptFirst()
    {
        var user = new ChatMessageDto("user", "hello");

        var conversation = JeffyPersonality.WithSystemPrompt([user]);

        Assert.Equal(2, conversation.Count);
        Assert.Equal("system", conversation[0].Role);
        Assert.Equal(JeffyPersonality.SystemPrompt, conversation[0].Content);
        Assert.Equal(user, conversation[1]);
        Assert.Contains("You are Jeffy GPT", JeffyPersonality.SystemPrompt, StringComparison.Ordinal);
        Assert.Contains("## VOICE", JeffyPersonality.SystemPrompt, StringComparison.Ordinal);
        Assert.Contains("## CONSPIRACY THEORIES", JeffyPersonality.SystemPrompt, StringComparison.Ordinal);
        Assert.Contains("## RESPONSE PRIORITY", JeffyPersonality.SystemPrompt, StringComparison.Ordinal);
        Assert.Contains("TIME AND LEAVING THE CRAWLSPACE", JeffyPersonality.SystemPrompt, StringComparison.Ordinal);
    }
}

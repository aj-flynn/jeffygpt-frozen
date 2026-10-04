namespace JeffyGPT.Infrastructure.Configuration;

public class OpenAiCompatibleOptions
{
    public string BaseUrl { get; set; } = "https://api.openai.com/v1";

    public string ApiKey { get; set; } = string.Empty;

    public string ApiKeyEnvironmentVariable { get; set; } = "OPENAI_API_KEY";

    public string Model { get; set; } = "gpt-5.4-nano";

    /// <summary>
    /// Optional chat/completions <c>reasoning_effort</c>. GPT-5.6 models reject
    /// function tools on chat/completions unless this is <c>none</c> (or the
    /// client uses the Responses API instead).
    /// </summary>
    public string? ReasoningEffort { get; set; }

    public bool RequireApiKey { get; set; } = true;

    public int TimeoutSeconds { get; set; } = 120;
}

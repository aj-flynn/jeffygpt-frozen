namespace JeffyGPT.Infrastructure.Configuration;

public class AssistantOptions
{
    public const string SectionName = "JeffyGPT:Assistant";

    public string Provider { get; set; } = "Fake";

    public OpenAiCompatibleOptions OpenAiCompatible { get; set; } = new();

    public bool IsOpenAiCompatibleProvider() =>
        string.Equals(Provider, "OpenAiCompatible", StringComparison.OrdinalIgnoreCase);
}

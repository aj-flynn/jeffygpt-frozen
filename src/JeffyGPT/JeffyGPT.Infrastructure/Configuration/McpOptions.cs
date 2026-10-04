namespace JeffyGPT.Infrastructure.Configuration;

public class McpOptions
{
    public const string SectionName = "JeffyGPT:Mcp";

    public List<McpServerOptions> Servers { get; set; } = [];
}

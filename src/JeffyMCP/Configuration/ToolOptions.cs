namespace JeffyMCP.Configuration;

public class ToolOptions
{
    public const string SectionName = "JeffyMCP:Tools";

    public FileSystemSandboxOptions FileSystem { get; set; } = new();
}

namespace JeffyMCP.Configuration;

public class FileSystemSandboxOptions
{
    public Dictionary<string, FileSystemRootOptions> Roots { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["docs"] = new FileSystemRootOptions
        {
            Path = "../../.roots/docs",
            Description = "JeffyGPT project documentation root",
            EntryPoint = "README.md"
        }
    };

    public long MaxReadBytes { get; set; } = 1_048_576;

    public List<string> AllowedExtensions { get; set; } =
    [
        ".md",
        ".txt",
        ".json",
        ".cs",
        ".ts",
        ".tsx",
        ".css"
    ];

    public IEnumerable<KeyValuePair<string, FileSystemRootOptions>> GetConfiguredRoots() =>
        Roots.Where(kvp => !string.IsNullOrWhiteSpace(kvp.Value.Path));
}

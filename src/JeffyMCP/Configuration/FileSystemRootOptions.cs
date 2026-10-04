namespace JeffyMCP.Configuration;

public sealed class FileSystemRootOptions
{
    public string Path { get; init; } = "";

    public string? Description { get; init; }

    public string? EntryPoint { get; init; }
}

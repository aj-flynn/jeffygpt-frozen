namespace JeffyMCP.FileSystem;

public sealed record FileSystemSandboxResolution(
    bool IsAllowed,
    string? FullPath = null,
    string? ErrorMessage = null)
{
    public static FileSystemSandboxResolution Allowed(string fullPath) => new(true, fullPath);

    public static FileSystemSandboxResolution Denied(string errorMessage) => new(false, ErrorMessage: errorMessage);
}

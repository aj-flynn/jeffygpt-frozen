namespace JeffyMCP.FileSystem;

public sealed record FileSystemSandboxListResult(
    bool IsAllowed,
    IReadOnlyList<string>? Files = null,
    string? ErrorMessage = null)
{
    public static FileSystemSandboxListResult Allowed(IReadOnlyList<string> files) =>
        new(true, files);

    public static FileSystemSandboxListResult Denied(string errorMessage) =>
        new(false, ErrorMessage: errorMessage);
}

namespace JeffyMCP.FileSystem;

public sealed record FileSystemSandboxReadResult(
    bool IsAllowed,
    string? Content = null,
    int BytesRead = 0,
    string? ErrorMessage = null)
{
    public static FileSystemSandboxReadResult Allowed(string content, int bytesRead) =>
        new(true, content, bytesRead);

    public static FileSystemSandboxReadResult Denied(string errorMessage) =>
        new(false, ErrorMessage: errorMessage);
}

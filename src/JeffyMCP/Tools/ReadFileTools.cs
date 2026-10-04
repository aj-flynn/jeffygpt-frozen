using System.ComponentModel;
using System.Text.Json;
using JeffyMCP.FileSystem;
using ModelContextProtocol.Server;

namespace JeffyMCP.Tools;

[McpServerToolType]
public class ReadFileTools
{
    private readonly FileSystemSandbox _fileSystemSandbox;

    public ReadFileTools(FileSystemSandbox fileSystemSandbox)
    {
        _fileSystemSandbox = fileSystemSandbox;
    }

    [McpServerTool(Name = "read-file")]
    [Description("Reads a text file from a configured sandboxed filesystem root.")]
    public async Task<string> ReadFile(
        string rootAlias,
        string relativePath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var readResult = await _fileSystemSandbox.ReadTextFileAsync(
            rootAlias,
            relativePath,
            cancellationToken);

        if (!readResult.IsAllowed)
        {
            return JsonSerializer.Serialize(new { error = readResult.ErrorMessage });
        }

        return JsonSerializer.Serialize(new
        {
            rootAlias,
            relativePath,
            content = readResult.Content,
            bytesRead = readResult.BytesRead
        });
    }
}

using System.ComponentModel;
using System.Text.Json;
using JeffyMCP.FileSystem;
using ModelContextProtocol.Server;

namespace JeffyMCP.Tools;

[McpServerToolType]
public class ListFilesTools
{
    private readonly FileSystemSandbox _fileSystemSandbox;

    public ListFilesTools(FileSystemSandbox fileSystemSandbox)
    {
        _fileSystemSandbox = fileSystemSandbox;
    }

    [McpServerTool(Name = "list-files")]
    [Description("Lists readable files in a configured sandboxed filesystem root. Use an empty relativePath to list the root directory.")]
    public string ListFiles(string rootAlias, string relativePath = "")
    {
        var listResult = _fileSystemSandbox.ListAllowedFiles(rootAlias, relativePath);
        if (!listResult.IsAllowed)
        {
            return JsonSerializer.Serialize(new { error = listResult.ErrorMessage });
        }

        return JsonSerializer.Serialize(new
        {
            rootAlias,
            relativePath,
            files = listResult.Files
        });
    }
}

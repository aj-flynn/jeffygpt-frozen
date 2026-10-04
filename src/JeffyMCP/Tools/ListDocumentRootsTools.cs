using System.ComponentModel;
using System.Text.Json;
using JeffyMCP.Configuration;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;

namespace JeffyMCP.Tools;

[McpServerToolType]
public class ListDocumentRootsTools
{
    private readonly ToolOptions _options;

    public ListDocumentRootsTools(IOptions<ToolOptions> options)
    {
        _options = options.Value;
    }

    [McpServerTool(Name = "list-document-roots")]
    [Description("Lists configured readable document roots. Use this before reading project documentation. If a root has an entryPoint, read that file first to understand the root contents.")]
    public string ListDocumentRoots()
    {
        var roots = _options.FileSystem.GetConfiguredRoots()
            .Select(kvp => BuildRootEntry(kvp.Key, kvp.Value))
            .ToArray();

        return JsonSerializer.Serialize(new { roots });
    }

    private static object BuildRootEntry(string alias, FileSystemRootOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.Description) && !string.IsNullOrWhiteSpace(options.EntryPoint))
        {
            return new { alias, description = options.Description, entryPoint = options.EntryPoint };
        }

        if (!string.IsNullOrWhiteSpace(options.Description))
        {
            return new { alias, description = options.Description };
        }

        if (!string.IsNullOrWhiteSpace(options.EntryPoint))
        {
            return new { alias, entryPoint = options.EntryPoint };
        }

        return new { alias };
    }
}

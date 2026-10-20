using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using JeffyMCP.Configuration;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;

namespace JeffyMCP.Tools;

[McpServerToolType]
public class ListDocumentRootsTools
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

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
            .Select(root => new DocumentRootEntry(
                root.Key,
                string.IsNullOrWhiteSpace(root.Value.Description) ? null : root.Value.Description,
                string.IsNullOrWhiteSpace(root.Value.EntryPoint) ? null : root.Value.EntryPoint))
            .ToArray();

        return JsonSerializer.Serialize(new { roots }, SerializerOptions);
    }

    private sealed record DocumentRootEntry(string Alias, string? Description, string? EntryPoint);
}

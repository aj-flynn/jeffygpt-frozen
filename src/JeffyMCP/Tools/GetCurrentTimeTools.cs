using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;

namespace JeffyMCP.Tools;

[McpServerToolType]
public static class GetCurrentTimeTools
{
    [McpServerTool(Name = "get-current-time"), Description("Gets the current local time.")]
    public static string GetCurrentTime()
    {
        return JsonSerializer.Serialize(new { local = DateTimeOffset.Now.ToString("O") });
    }
}

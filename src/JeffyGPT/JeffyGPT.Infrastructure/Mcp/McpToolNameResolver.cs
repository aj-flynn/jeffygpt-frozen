using JeffyGPT.Infrastructure.Configuration;

namespace JeffyGPT.Infrastructure.Mcp;

internal static class McpToolNameResolver
{
    public static string ResolveExposedName(
        string serverId,
        string originalToolName,
        int enabledServerCount)
    {
        if (enabledServerCount <= 1)
        {
            return originalToolName;
        }

        return $"{serverId}__{originalToolName}";
    }

    public static void ValidateNoDuplicateExposedNames(IEnumerable<McpDiscoveredTool> tools)
    {
        var duplicates = tools
            .GroupBy(tool => tool.ExposedToolName, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();

        if (duplicates.Count > 0)
        {
            throw new InvalidOperationException(
                $"Duplicate MCP tool names after prefixing: {string.Join(", ", duplicates)}");
        }
    }
}

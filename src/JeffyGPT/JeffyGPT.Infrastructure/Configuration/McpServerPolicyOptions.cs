namespace JeffyGPT.Infrastructure.Configuration;

public class McpServerPolicyOptions
{
    /// <summary>
    /// When true, every tool discovered from this server is registered and executable
    /// with no further per-tool checks.
    /// </summary>
    public bool AllowAllTools { get; set; }

    /// <summary>
    /// Original MCP tool names permitted for discovery and execution.
    /// Required when <see cref="AllowAllTools"/> is false.
    /// </summary>
    public List<string> AllowedTools { get; set; } = [];

    public bool AllowsTool(string originalToolName) =>
        AllowAllTools || IsOnApprovedList(originalToolName);

    public bool IsValid(out string error)
    {
        if (AllowAllTools || HasApprovedToolList())
        {
            error = "";
            return true;
        }

        error = "When AllowAllTools is false, Policy.AllowedTools must list at least one approved tool.";
        return false;
    }

    private bool HasApprovedToolList() =>
        AllowedTools.Any(HasToolName);

    private bool IsOnApprovedList(string originalToolName) =>
        AllowedTools.Any(name =>
            string.Equals(name, originalToolName, StringComparison.OrdinalIgnoreCase));

    private static bool HasToolName(string? name) =>
        !string.IsNullOrWhiteSpace(name);
}

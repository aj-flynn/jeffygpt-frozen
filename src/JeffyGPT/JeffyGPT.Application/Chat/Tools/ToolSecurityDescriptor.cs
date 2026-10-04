namespace JeffyGPT.Application.Chat.Tools;

public sealed record ToolSecurityDescriptor(
    ToolAccessKind AccessKind,
    ToolDataExposure DataExposure,
    bool RequiresConfirmation,
    string? SafetyNotes = null)
{
    /// <summary>
    /// Label for a tool proxied from an MCP server.
    /// The real allowlist is the server policy. This descriptor is not a second gate,
    /// and <see cref="ToolDataExposure"/> is not consulted by the default execution policy.
    /// </summary>
    public static ToolSecurityDescriptor ProxiedMcpTool() =>
        new(ToolAccessKind.ReadOnly, ToolDataExposure.None, RequiresConfirmation: false);
}

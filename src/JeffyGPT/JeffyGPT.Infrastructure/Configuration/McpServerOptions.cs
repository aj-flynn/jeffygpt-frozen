namespace JeffyGPT.Infrastructure.Configuration;

public class McpServerOptions
{
    public string Id { get; set; } = "";

    public bool Enabled { get; set; } = true;

    public bool Required { get; set; } = true;

    public McpTransportKind Transport { get; set; } = McpTransportKind.Stdio;

    public string? Command { get; set; }

    public List<string> Arguments { get; set; } = [];

    /// <summary>
    /// Optional working directory for stdio processes. Relative paths are resolved against the API content root.
    /// </summary>
    public string? WorkingDirectory { get; set; }

    public string? Endpoint { get; set; }

    public McpServerPolicyOptions Policy { get; set; } = new();

    public bool IsConnectable() =>
        Enabled && !string.IsNullOrWhiteSpace(Id);
}

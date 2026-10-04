namespace JeffyMCP.Http;

public static class JeffyMcpHttpBindRules
{
    public const string DefaultLoopbackUrl = "http://127.0.0.1:3001";
    public const string AllowNonLoopbackConfigKey = "JeffyMCP:AllowNonLoopbackHttp";

    /// <summary>
    /// Second required flag for non-loopback binds. Non-loopback MCP HTTP has no auth today,
    /// so both this and <see cref="AllowNonLoopbackConfigKey"/> must be true.
    /// </summary>
    public const string AllowUnauthenticatedNonLoopbackConfigKey =
        "JeffyMCP:AllowUnauthenticatedNonLoopbackHttp";

    public static bool IsLoopbackHost(string host) =>
        string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)
        || string.Equals(host, "127.0.0.1", StringComparison.OrdinalIgnoreCase)
        || string.Equals(host, "::1", StringComparison.OrdinalIgnoreCase)
        || string.Equals(host, "[::1]", StringComparison.OrdinalIgnoreCase);

    public static bool IsLoopbackUrl(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && (uri.IsLoopback || IsLoopbackHost(uri.Host));

    /// <summary>
    /// Ensures every semicolon-separated URL is loopback unless both non-loopback opt-in flags are set.
    /// </summary>
    public static void EnsureLoopbackOnly(
        IEnumerable<string> urls,
        bool allowNonLoopback,
        bool allowUnauthenticatedNonLoopback = false)
    {
        var nonLoopbackPermitted = allowNonLoopback && allowUnauthenticatedNonLoopback;
        if (nonLoopbackPermitted)
        {
            return;
        }

        foreach (var url in urls)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                continue;
            }

            if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri))
            {
                throw new InvalidOperationException($"JeffyMCP HTTP URL is invalid: '{url}'.");
            }

            if (!uri.IsLoopback && !IsLoopbackHost(uri.Host))
            {
                throw new InvalidOperationException(
                    $"JeffyMCP HTTP must bind to loopback only (got '{uri.Host}'). " +
                    $"Set {AllowNonLoopbackConfigKey}=true and " +
                    $"{AllowUnauthenticatedNonLoopbackConfigKey}=true to override (no MCP auth yet).");
            }
        }
    }

    public static IEnumerable<string> SplitUrls(string? urls) =>
        (urls ?? string.Empty).Split(
            ';',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}

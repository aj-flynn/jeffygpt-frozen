using JeffyMCP.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace JeffyMCP.Tests;

internal sealed class TestHostEnvironment(string contentRoot) : IHostEnvironment
{
    public string EnvironmentName { get; set; } = Environments.Development;

    public string ApplicationName { get; set; } = "JeffyMCP.Tests";

    public string ContentRootPath { get; set; } = contentRoot;

    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}

internal static class TestToolOptions
{
    public static ToolOptions Create(
        string docsRelativePath = "docs-root",
        long maxReadBytes = 1_048_576) =>
        new()
        {
            FileSystem = new FileSystemSandboxOptions
            {
                Roots = new Dictionary<string, FileSystemRootOptions>(StringComparer.OrdinalIgnoreCase)
                {
                    ["docs"] = new FileSystemRootOptions { Path = docsRelativePath }
                },
                MaxReadBytes = maxReadBytes,
                AllowedExtensions = [".md", ".txt", ".json"]
            }
        };
}

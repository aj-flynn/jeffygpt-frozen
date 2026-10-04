using JeffyGPT.Infrastructure.Configuration;
using JeffyGPT.Infrastructure.Mcp;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Client;

namespace JeffyGPT.Application.Tests;

public class McpConnectionManagerTests
{
    [Fact]
    public void CreateStdioTransport_MissingCommand_Throws()
    {
        using var layout = StdioLaunchLayout.Create();
        var manager = layout.CreateManager();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            manager.CreateStdioTransport(layout.Server(command: "   ")));

        Assert.Contains("requires Command", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CreateStdioTransport_RelativeProjectBesideContentRoot_AllowsStartup()
    {
        using var layout = StdioLaunchLayout.Create();
        layout.WriteProject(layout.ContentRoot);
        var manager = layout.CreateManager();

        var transport = manager.CreateStdioTransport(layout.Server(arguments: ["JeffyMCP.csproj"]));

        Assert.IsType<StdioClientTransport>(transport);
    }

    [Fact]
    public void CreateStdioTransport_RelativeProjectOnlyInWorkingDirectory_AllowsStartup()
    {
        using var layout = StdioLaunchLayout.Create();
        layout.WriteProject(layout.WorkingDirectory);
        var manager = layout.CreateManager();

        var transport = manager.CreateStdioTransport(layout.Server(arguments: ["JeffyMCP.csproj"]));

        Assert.IsType<StdioClientTransport>(transport);
    }

    [Fact]
    public void CreateStdioTransport_MissingProject_NamesTheContentRootPath()
    {
        using var layout = StdioLaunchLayout.Create();
        var manager = layout.CreateManager();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            manager.CreateStdioTransport(layout.Server(arguments: ["JeffyMCP.csproj"])));

        Assert.Contains(layout.ContentRootProjectPath, ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(layout.WorkingDirectoryProjectPath, ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CreateStdioTransport_FlagArgument_IsNotTreatedAsAFile()
    {
        using var layout = StdioLaunchLayout.Create();
        var manager = layout.CreateManager();

        var transport = manager.CreateStdioTransport(layout.Server(arguments: ["--no-build"]));

        Assert.IsType<StdioClientTransport>(transport);
    }

    [Fact]
    public void CreateStdioTransport_AbsoluteProjectPath_IsUsedAsGiven()
    {
        using var layout = StdioLaunchLayout.Create();
        var projectPath = layout.WriteProject(layout.ContentRoot);
        var manager = layout.CreateManager();

        var transport = manager.CreateStdioTransport(layout.Server(arguments: [projectPath]));

        Assert.IsType<StdioClientTransport>(transport);
    }

    /// <summary>
    /// Two sibling folders. Tests plant a project file in one of them and never start a process.
    /// </summary>
    private sealed class StdioLaunchLayout : IDisposable
    {
        private StdioLaunchLayout(string root)
        {
            Root = root;
            ContentRoot = Path.Combine(root, "content");
            WorkingDirectory = Path.Combine(root, "work");
            Directory.CreateDirectory(ContentRoot);
            Directory.CreateDirectory(WorkingDirectory);
        }

        public string Root { get; }

        public string ContentRoot { get; }

        public string WorkingDirectory { get; }

        public string ContentRootProjectPath =>
            Path.GetFullPath(Path.Combine(ContentRoot, "JeffyMCP.csproj"));

        public string WorkingDirectoryProjectPath =>
            Path.GetFullPath(Path.Combine(WorkingDirectory, "JeffyMCP.csproj"));

        public static StdioLaunchLayout Create()
        {
            var root = Path.Combine(Path.GetTempPath(), "jeffygpt-mcp-" + Guid.NewGuid().ToString("N"));
            return new StdioLaunchLayout(root);
        }

        public McpConnectionManager CreateManager() =>
            new(
                Options.Create(new McpOptions()),
                new ContentRootEnvironment(ContentRoot),
                NullLogger<McpConnectionManager>.Instance);

        public McpServerOptions Server(string command = "dotnet", params string[] arguments) =>
            new()
            {
                Id = "jeffymcp",
                Transport = McpTransportKind.Stdio,
                Command = command,
                WorkingDirectory = WorkingDirectory,
                Arguments = arguments.ToList(),
            };

        public string WriteProject(string directory)
        {
            var path = Path.Combine(directory, "JeffyMCP.csproj");
            File.WriteAllText(path, "<Project />");
            return path;
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }

    private sealed class ContentRootEnvironment(string contentRoot) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;

        public string ApplicationName { get; set; } = "JeffyGPT.Application.Tests";

        public string ContentRootPath { get; set; } = contentRoot;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}

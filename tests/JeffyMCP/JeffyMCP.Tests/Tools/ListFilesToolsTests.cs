using System.Text.Json;
using JeffyMCP.Configuration;
using JeffyMCP.FileSystem;
using JeffyMCP.Tools;
using Microsoft.Extensions.Options;

namespace JeffyMCP.Tests.Tools;

public class ListFilesToolsTests : IDisposable
{
    private readonly string _contentRoot;
    private readonly string _docsRoot;

    public ListFilesToolsTests()
    {
        _contentRoot = Path.Combine(Path.GetTempPath(), "JeffyMCP.ListFilesToolsTests", Guid.NewGuid().ToString("N"));
        _docsRoot = Path.Combine(_contentRoot, "docs-root");
        Directory.CreateDirectory(_docsRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_contentRoot))
        {
            Directory.Delete(_contentRoot, recursive: true);
        }
    }

    [Fact]
    public void ListFiles_RootDirectory_ReturnsAllowedFiles()
    {
        File.WriteAllText(Path.Combine(_docsRoot, "readme.md"), "hello");
        File.WriteAllText(Path.Combine(_docsRoot, "ignore.exe"), "binary");

        var tools = CreateTools();
        var result = tools.ListFiles("docs", "");

        using var document = JsonDocument.Parse(result);
        var files = document.RootElement.GetProperty("files").EnumerateArray().Select(e => e.GetString()).ToList();
        Assert.Contains("readme.md", files);
        Assert.DoesNotContain("ignore.exe", files);
    }

    private ListFilesTools CreateTools()
    {
        var options = TestToolOptions.Create();
        var sandbox = new FileSystemSandbox(Options.Create(options), new TestHostEnvironment(_contentRoot));
        return new ListFilesTools(sandbox);
    }
}

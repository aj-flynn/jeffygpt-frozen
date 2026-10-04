using System.Text;
using System.Text.Json;
using JeffyMCP.Configuration;
using JeffyMCP.FileSystem;
using JeffyMCP.Tools;
using Microsoft.Extensions.Options;

namespace JeffyMCP.Tests.Tools;

public class ReadFileToolsTests : IDisposable
{
    private readonly string _contentRoot;
    private readonly string _docsRoot;

    public ReadFileToolsTests()
    {
        _contentRoot = Path.Combine(Path.GetTempPath(), "JeffyMCP.ReadFileToolsTests", Guid.NewGuid().ToString("N"));
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
    public async Task ReadFile_ValidSandboxedFile_ReturnsContent()
    {
        const string relativePath = "hello.md";
        const string fileContent = "hello from sandbox";
        File.WriteAllText(Path.Combine(_docsRoot, relativePath), fileContent);

        var tools = CreateTools();
        var result = await tools.ReadFile("docs", relativePath, CancellationToken.None);

        using var document = JsonDocument.Parse(result);
        Assert.Equal("docs", document.RootElement.GetProperty("rootAlias").GetString());
        Assert.Equal(relativePath, document.RootElement.GetProperty("relativePath").GetString());
        Assert.Equal(fileContent, document.RootElement.GetProperty("content").GetString());
        Assert.Equal(Encoding.UTF8.GetByteCount(fileContent), document.RootElement.GetProperty("bytesRead").GetInt32());
    }

    [Fact]
    public async Task ReadFile_SandboxDenied_ReturnsErrorJson()
    {
        var tools = CreateTools();
        var result = await tools.ReadFile("unknown", "hello.md", CancellationToken.None);

        using var document = JsonDocument.Parse(result);
        Assert.Equal("Unknown root alias", document.RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public async Task ReadFile_DoesNotExposeAbsolutePaths()
    {
        const string relativePath = "secret.md";
        File.WriteAllText(Path.Combine(_docsRoot, relativePath), "content");

        var tools = CreateTools();

        var validResult = await tools.ReadFile("docs", relativePath, CancellationToken.None);
        Assert.DoesNotContain(_contentRoot, validResult, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(_docsRoot, validResult, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(":\\", validResult, StringComparison.Ordinal);

        var deniedResult = await tools.ReadFile("docs", "../outside.md", CancellationToken.None);
        Assert.DoesNotContain(_contentRoot, deniedResult, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(_docsRoot, deniedResult, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(":\\", deniedResult, StringComparison.Ordinal);
    }

    private ReadFileTools CreateTools()
    {
        var options = TestToolOptions.Create();
        var sandbox = new FileSystemSandbox(Options.Create(options), new TestHostEnvironment(_contentRoot));
        return new ReadFileTools(sandbox);
    }
}

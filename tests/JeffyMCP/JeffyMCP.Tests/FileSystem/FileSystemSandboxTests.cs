using JeffyMCP.Configuration;
using JeffyMCP.FileSystem;
using Microsoft.Extensions.Options;

namespace JeffyMCP.Tests.FileSystem;

public class FileSystemSandboxTests : IDisposable
{
    private readonly string _contentRoot;
    private readonly string _docsRoot;

    public FileSystemSandboxTests()
    {
        _contentRoot = Path.Combine(Path.GetTempPath(), "JeffyMCP.FileSystemSandboxTests", Guid.NewGuid().ToString("N"));
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
    public void ResolveReadPath_ValidRelativeFile_Allows()
    {
        var relativePath = "readme.md";
        File.WriteAllText(Path.Combine(_docsRoot, relativePath), "hello");

        var sandbox = CreateSandbox();
        var result = sandbox.ResolveReadPath("docs", relativePath);

        Assert.True(result.IsAllowed);
        Assert.NotNull(result.FullPath);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void ResolveReadPath_AbsolutePath_Denies()
    {
        var sandbox = CreateSandbox();
        var absolutePath = Path.Combine(_docsRoot, "readme.md");

        var result = sandbox.ResolveReadPath("docs", absolutePath);

        Assert.False(result.IsAllowed);
        Assert.Null(result.FullPath);
        Assert.Equal("Absolute paths are not allowed", result.ErrorMessage);
    }

    [Fact]
    public void ResolveReadPath_PathTraversal_Denies()
    {
        var sandbox = CreateSandbox();

        var result = sandbox.ResolveReadPath("docs", "../outside.md");

        Assert.False(result.IsAllowed);
        Assert.Equal("Path traversal is not allowed", result.ErrorMessage);
    }

    [Fact]
    public void ResolveReadPath_UnknownRootAlias_Denies()
    {
        var sandbox = CreateSandbox();

        var result = sandbox.ResolveReadPath("unknown", "readme.md");

        Assert.False(result.IsAllowed);
        Assert.Equal("Unknown root alias", result.ErrorMessage);
    }

    [Fact]
    public void ResolveReadPath_DisallowedExtension_Denies()
    {
        var relativePath = "script.exe";
        File.WriteAllText(Path.Combine(_docsRoot, relativePath), "binary");

        var sandbox = CreateSandbox();
        var result = sandbox.ResolveReadPath("docs", relativePath);

        Assert.False(result.IsAllowed);
        Assert.Equal("File extension is not allowed", result.ErrorMessage);
    }

    [Fact]
    public void ResolveReadPath_OversizedFile_Denies()
    {
        var relativePath = "large.md";
        var filePath = Path.Combine(_docsRoot, relativePath);
        using (var stream = File.Create(filePath))
        {
            stream.SetLength(2_048);
        }

        var sandbox = CreateSandbox(maxReadBytes: 1_024);
        var result = sandbox.ResolveReadPath("docs", relativePath);

        Assert.False(result.IsAllowed);
        Assert.Equal("File exceeds maximum allowed size", result.ErrorMessage);
    }

    [Fact]
    public void ResolveReadPath_Directory_Denies()
    {
        var relativePath = "nested";
        Directory.CreateDirectory(Path.Combine(_docsRoot, relativePath));

        var sandbox = CreateSandbox();
        var result = sandbox.ResolveReadPath("docs", relativePath);

        Assert.False(result.IsAllowed);
        Assert.Equal("Only files are allowed", result.ErrorMessage);
    }

    [Fact]
    public void ResolveReadDirectory_ValidRelativeDirectory_Allows()
    {
        var relativePath = "nested";
        Directory.CreateDirectory(Path.Combine(_docsRoot, relativePath));

        var sandbox = CreateSandbox();
        var result = sandbox.ResolveReadDirectory("docs", relativePath);

        Assert.True(result.IsAllowed);
        Assert.NotNull(result.FullPath);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void ResolveReadDirectory_EmptyRelativePath_ResolvesRootDirectory()
    {
        var sandbox = CreateSandbox();
        var result = sandbox.ResolveReadDirectory("docs", "");

        Assert.True(result.IsAllowed);
        Assert.NotNull(result.FullPath);
        Assert.Equal(Path.GetFullPath(_docsRoot), result.FullPath);
    }

    [Fact]
    public void ResolveReadDirectory_AbsolutePath_Denies()
    {
        var sandbox = CreateSandbox();
        var absolutePath = Path.Combine(_docsRoot, "nested");

        var result = sandbox.ResolveReadDirectory("docs", absolutePath);

        Assert.False(result.IsAllowed);
        Assert.Equal("Absolute paths are not allowed", result.ErrorMessage);
    }

    [Fact]
    public void ResolveReadDirectory_DriveLetterPath_Denies()
    {
        var sandbox = CreateSandbox();

        var result = sandbox.ResolveReadDirectory("docs", "C:file.md");

        Assert.False(result.IsAllowed);
        Assert.Equal("Absolute paths are not allowed", result.ErrorMessage);
    }

    [Fact]
    public void ResolveReadDirectory_PathTraversal_Denies()
    {
        var sandbox = CreateSandbox();

        var result = sandbox.ResolveReadDirectory("docs", "../outside");

        Assert.False(result.IsAllowed);
        Assert.Equal("Path traversal is not allowed", result.ErrorMessage);
    }

    [Fact]
    public void ResolveReadDirectory_UnknownRootAlias_Denies()
    {
        var sandbox = CreateSandbox();

        var result = sandbox.ResolveReadDirectory("unknown", "");

        Assert.False(result.IsAllowed);
        Assert.Equal("Unknown root alias", result.ErrorMessage);
    }

    [Fact]
    public void ResolveReadDirectory_FilePath_Denies()
    {
        var relativePath = "readme.md";
        File.WriteAllText(Path.Combine(_docsRoot, relativePath), "hello");

        var sandbox = CreateSandbox();
        var result = sandbox.ResolveReadDirectory("docs", relativePath);

        Assert.False(result.IsAllowed);
        Assert.Equal("Only directories are allowed", result.ErrorMessage);
    }

    [Fact]
    public async Task ReadTextFileAsync_ValidFile_ReturnsContent()
    {
        const string relativePath = "hello.md";
        File.WriteAllText(Path.Combine(_docsRoot, relativePath), "hello");

        var sandbox = CreateSandbox();
        var result = await sandbox.ReadTextFileAsync("docs", relativePath, CancellationToken.None);

        Assert.True(result.IsAllowed);
        Assert.Equal("hello", result.Content);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public async Task ReadTextFileAsync_OversizedFile_DeniesWithoutPathLeak()
    {
        var relativePath = "large.md";
        var filePath = Path.Combine(_docsRoot, relativePath);
        using (var stream = File.Create(filePath))
        {
            stream.SetLength(2_048);
        }

        var sandbox = CreateSandbox(maxReadBytes: 1_024);
        var result = await sandbox.ReadTextFileAsync("docs", relativePath, CancellationToken.None);

        Assert.False(result.IsAllowed);
        Assert.Equal("File exceeds maximum allowed size", result.ErrorMessage);
        Assert.DoesNotContain(_docsRoot, result.ErrorMessage!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ResolveReadPath_NullRootAlias_Denies()
    {
        var sandbox = CreateSandbox();
        var result = sandbox.ResolveReadPath(null!, "readme.md");

        Assert.False(result.IsAllowed);
        Assert.Equal("Root alias is required", result.ErrorMessage);
    }

    [Fact]
    public void ResolveReadDirectory_Symlink_Denies()
    {
        var targetDir = Path.Combine(_docsRoot, "target");
        var linkPath = Path.Combine(_docsRoot, "linked");
        Directory.CreateDirectory(targetDir);

        try
        {
            Directory.CreateSymbolicLink(linkPath, targetDir);
        }
        catch (IOException)
        {
            return;
        }
        catch (UnauthorizedAccessException)
        {
            return;
        }

        var sandbox = CreateSandbox();
        var result = sandbox.ResolveReadDirectory("docs", "linked");

        Assert.False(result.IsAllowed);
        Assert.Equal("Symbolic links are not allowed", result.ErrorMessage);
    }

    private FileSystemSandbox CreateSandbox(long maxReadBytes = 1_048_576) =>
        new(
            Options.Create(TestToolOptions.Create(maxReadBytes: maxReadBytes)),
            new TestHostEnvironment(_contentRoot));
}

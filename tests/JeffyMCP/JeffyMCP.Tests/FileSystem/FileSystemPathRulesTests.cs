using JeffyMCP.FileSystem;

namespace JeffyMCP.Tests.FileSystem;

public class FileSystemPathRulesTests
{
    [Theory]
    [InlineData("readme.md")]
    [InlineData("nested/readme.md")]
    [InlineData("./readme.md")]
    public void ContainsAbsolutePathSyntax_RelativePath_ReturnsFalse(string relativePath)
    {
        Assert.False(FileSystemSandbox.ContainsAbsolutePathSyntax(relativePath));
    }

    [Fact]
    public void ContainsAbsolutePathSyntax_RootedPath_ReturnsTrue()
    {
        var rootedPath = Path.Combine("docs-root", "readme.md");
        if (!Path.IsPathRooted(rootedPath))
        {
            rootedPath = Path.DirectorySeparatorChar + rootedPath;
        }

        Assert.True(FileSystemSandbox.ContainsAbsolutePathSyntax(rootedPath));
    }

    [Fact]
    public void ContainsAbsolutePathSyntax_DriveLetterPath_ReturnsTrue()
    {
        Assert.True(FileSystemSandbox.ContainsAbsolutePathSyntax("C:file.md"));
    }

    [Fact]
    public void ClassifyReadTarget_ExistingFile_ReturnsFile()
    {
        var filePath = Path.GetTempFileName();

        try
        {
            Assert.Equal(ReadPathTargetKind.File, FileSystemSandbox.ClassifyReadTarget(filePath));
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void ClassifyReadTarget_ExistingDirectory_ReturnsDirectory()
    {
        var directoryPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directoryPath);

        try
        {
            Assert.Equal(ReadPathTargetKind.Directory, FileSystemSandbox.ClassifyReadTarget(directoryPath));
        }
        finally
        {
            Directory.Delete(directoryPath);
        }
    }

    [Fact]
    public void ClassifyReadTarget_MissingPath_ReturnsMissing()
    {
        var missingPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

        Assert.Equal(ReadPathTargetKind.Missing, FileSystemSandbox.ClassifyReadTarget(missingPath));
    }
}

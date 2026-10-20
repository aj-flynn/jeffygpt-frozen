using System.Text.Json;
using JeffyMCP.Configuration;
using JeffyMCP.Tools;
using Microsoft.Extensions.Options;

namespace JeffyMCP.Tests.Tools;

public class ListDocumentRootsToolsTests
{
    [Theory]
    [InlineData(null, null)]
    [InlineData(" ", "\t")]
    [InlineData("Description", null)]
    [InlineData(null, "README.md")]
    [InlineData("Description", "README.md")]
    public void ListDocumentRoots_OmitsMissingMetadata(string? description, string? entryPoint)
    {
        var tools = new ListDocumentRootsTools(Options.Create(new ToolOptions
        {
            FileSystem = new FileSystemSandboxOptions
            {
                Roots = new Dictionary<string, FileSystemRootOptions>
                {
                    ["docs"] = new()
                    {
                        Path = ".roots/docs",
                        Description = description,
                        EntryPoint = entryPoint
                    }
                }
            }
        }));

        using var document = JsonDocument.Parse(tools.ListDocumentRoots());
        var root = document.RootElement.GetProperty("roots")[0];

        Assert.Equal("docs", root.GetProperty("alias").GetString());
        Assert.Equal(!string.IsNullOrWhiteSpace(description), root.TryGetProperty("description", out var descriptionValue));
        Assert.Equal(!string.IsNullOrWhiteSpace(entryPoint), root.TryGetProperty("entryPoint", out var entryPointValue));
        if (!string.IsNullOrWhiteSpace(description))
        {
            Assert.Equal(description, descriptionValue.GetString());
        }
        if (!string.IsNullOrWhiteSpace(entryPoint))
        {
            Assert.Equal(entryPoint, entryPointValue.GetString());
        }
    }

    [Fact]
    public void ListDocumentRoots_ReturnsConfiguredRootMetadataWithoutAbsolutePath()
    {
        const string description = "JeffyGPT project documentation root";
        const string entryPoint = "README.md";
        var tools = new ListDocumentRootsTools(Options.Create(new ToolOptions
        {
            FileSystem = new FileSystemSandboxOptions
            {
                Roots = new Dictionary<string, FileSystemRootOptions>(StringComparer.OrdinalIgnoreCase)
                {
                    ["docs"] = new FileSystemRootOptions
                    {
                        Path = "../../.roots/docs",
                        Description = description,
                        EntryPoint = entryPoint
                    }
                }
            }
        }));

        var result = tools.ListDocumentRoots();

        Assert.DoesNotContain(":\\", result, StringComparison.Ordinal);
        Assert.DoesNotContain("../../.roots/docs", result, StringComparison.Ordinal);

        using var document = JsonDocument.Parse(result);
        var roots = document.RootElement.GetProperty("roots");
        Assert.Equal(1, roots.GetArrayLength());

        var root = roots[0];
        Assert.Equal("docs", root.GetProperty("alias").GetString());
        Assert.Equal(description, root.GetProperty("description").GetString());
        Assert.Equal(entryPoint, root.GetProperty("entryPoint").GetString());
    }

    [Fact]
    public void ListDocumentRoots_SkipsRootsWithEmptyPath()
    {
        var tools = new ListDocumentRootsTools(Options.Create(new ToolOptions
        {
            FileSystem = new FileSystemSandboxOptions
            {
                Roots = new Dictionary<string, FileSystemRootOptions>(StringComparer.OrdinalIgnoreCase)
                {
                    ["docs"] = new FileSystemRootOptions
                    {
                        Path = "docs-root",
                        Description = "Visible root"
                    },
                    ["empty"] = new FileSystemRootOptions
                    {
                        Path = "",
                        Description = "Should not appear"
                    }
                }
            }
        }));

        using var document = JsonDocument.Parse(tools.ListDocumentRoots());
        var roots = document.RootElement.GetProperty("roots");
        Assert.Equal(1, roots.GetArrayLength());
        Assert.Equal("docs", roots[0].GetProperty("alias").GetString());
    }
}

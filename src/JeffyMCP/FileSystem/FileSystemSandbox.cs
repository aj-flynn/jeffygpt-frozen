using System.Text;
using JeffyMCP.Configuration;
using Microsoft.Extensions.Options;

namespace JeffyMCP.FileSystem;

public class FileSystemSandbox
{
    public const int MaxRootAliasLength = 256;
    public const int MaxRelativePathLength = 1024;

    private readonly ToolOptions _options;
    private readonly string _contentRoot;

    public FileSystemSandbox(IOptions<ToolOptions> options, IHostEnvironment hostEnvironment)
    {
        _options = options.Value;
        _contentRoot = hostEnvironment.ContentRootPath;
    }

    public FileSystemSandboxResolution ResolveReadPath(string rootAlias, string relativePath)
    {
        if (!IsValidRootAlias(rootAlias, out var aliasError))
        {
            return FileSystemSandboxResolution.Denied(aliasError!);
        }

        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return FileSystemSandboxResolution.Denied("Path is required");
        }

        if (relativePath.Length > MaxRelativePathLength)
        {
            return FileSystemSandboxResolution.Denied("Path is too long");
        }

        var resolution = TryResolveCandidatePath(rootAlias, relativePath);
        if (!resolution.IsAllowed)
        {
            return resolution;
        }

        var candidateFullPath = resolution.FullPath!;

        return ClassifyReadTarget(candidateFullPath) switch
        {
            ReadPathTargetKind.File => ResolveAllowedReadFile(candidateFullPath),
            ReadPathTargetKind.Directory => FileSystemSandboxResolution.Denied("Only files are allowed"),
            _ => FileSystemSandboxResolution.Denied("File was not found")
        };
    }

    public FileSystemSandboxResolution ResolveReadDirectory(string rootAlias, string relativePath)
    {
        if (!IsValidRootAlias(rootAlias, out var aliasError))
        {
            return FileSystemSandboxResolution.Denied(aliasError!);
        }

        var normalizedRelativePath = string.IsNullOrWhiteSpace(relativePath) ? string.Empty : relativePath;
        if (normalizedRelativePath.Length > MaxRelativePathLength)
        {
            return FileSystemSandboxResolution.Denied("Path is too long");
        }

        if (IsListingConfiguredRoot(normalizedRelativePath))
        {
            return ResolveConfiguredRootDirectory(rootAlias);
        }

        return ResolveSubdirectory(rootAlias, normalizedRelativePath);
    }

    private static bool IsListingConfiguredRoot(string normalizedRelativePath) =>
        string.IsNullOrEmpty(normalizedRelativePath);

    private FileSystemSandboxResolution ResolveSubdirectory(string rootAlias, string relativePath)
    {
        var resolution = TryResolveCandidatePath(rootAlias, relativePath);
        if (!resolution.IsAllowed)
        {
            return resolution;
        }

        var candidateFullPath = resolution.FullPath!;

        if (File.Exists(candidateFullPath))
        {
            return FileSystemSandboxResolution.Denied("Only directories are allowed");
        }

        if (!Directory.Exists(candidateFullPath))
        {
            return FileSystemSandboxResolution.Denied("Directory was not found");
        }

        return FileSystemSandboxResolution.Allowed(candidateFullPath);
    }

    private FileSystemSandboxResolution ResolveConfiguredRootDirectory(string rootAlias)
    {
        if (!TryResolveRoot(rootAlias, out var root))
        {
            return FileSystemSandboxResolution.Denied("Unknown root alias");
        }

        if (!Directory.Exists(root.FullPath))
        {
            return FileSystemSandboxResolution.Denied("Directory was not found");
        }

        if (HasReparsePointInPath(root.FullPath, root.FullPath))
        {
            return FileSystemSandboxResolution.Denied("Symbolic links are not allowed");
        }

        return FileSystemSandboxResolution.Allowed(root.FullPath);
    }

    public bool IsAllowedExtension(string extension)
    {
        if (string.IsNullOrEmpty(extension))
        {
            return false;
        }

        return _options.FileSystem.AllowedExtensions.Any(
            allowed => string.Equals(allowed, extension, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Opens and reads a sandboxed text file with a hard byte cap and post-open reparse checks.
    /// </summary>
    public async Task<FileSystemSandboxReadResult> ReadTextFileAsync(
        string rootAlias,
        string relativePath,
        CancellationToken cancellationToken = default)
    {
        var resolution = ResolveReadPath(rootAlias, relativePath);
        if (!resolution.IsAllowed)
        {
            return FileSystemSandboxReadResult.Denied(resolution.ErrorMessage!);
        }

        var fullPath = resolution.FullPath!;
        if (!TryResolveRoot(rootAlias, out var root))
        {
            return FileSystemSandboxReadResult.Denied("Unknown root alias");
        }

        try
        {
            await using var stream = new FileStream(
                fullPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 8192,
                FileOptions.Asynchronous | FileOptions.SequentialScan);

            var openedPath = Path.GetFullPath(stream.Name);
            if (!IsPathWithinRoot(openedPath, root.FullPath) || HasReparsePointInPath(openedPath, root.FullPath))
            {
                return FileSystemSandboxReadResult.Denied("Symbolic links are not allowed");
            }

            var content = await ReadUpToMaxBytesAsync(stream, _options.FileSystem.MaxReadBytes, cancellationToken);
            if (content is null)
            {
                return FileSystemSandboxReadResult.Denied("File exceeds maximum allowed size");
            }

            return FileSystemSandboxReadResult.Allowed(content, Encoding.UTF8.GetByteCount(content));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return FileSystemSandboxReadResult.Denied("Failed to read file");
        }
    }

    public FileSystemSandboxListResult ListAllowedFiles(string rootAlias, string relativePath)
    {
        var resolution = ResolveReadDirectory(rootAlias, relativePath);
        if (!resolution.IsAllowed)
        {
            return FileSystemSandboxListResult.Denied(resolution.ErrorMessage!);
        }

        if (!TryResolveRoot(rootAlias, out var root))
        {
            return FileSystemSandboxListResult.Denied("Unknown root alias");
        }

        try
        {
            var directoryPath = resolution.FullPath!;
            if (HasReparsePointInPath(directoryPath, root.FullPath))
            {
                return FileSystemSandboxListResult.Denied("Symbolic links are not allowed");
            }

            var files = new List<string>();
            foreach (var path in Directory.GetFiles(directoryPath, "*", SearchOption.TopDirectoryOnly))
            {
                if (TryGetListableFileName(rootAlias, relativePath, path, out var fileName))
                {
                    files.Add(fileName);
                }
            }

            files.Sort(StringComparer.Ordinal);

            return FileSystemSandboxListResult.Allowed(files);
        }
        catch (Exception)
        {
            return FileSystemSandboxListResult.Denied("Failed to list files");
        }
    }

    private bool TryGetListableFileName(
        string rootAlias,
        string relativePath,
        string path,
        out string fileName)
    {
        fileName = Path.GetFileName(path);
        if (string.IsNullOrEmpty(fileName) || HasReparsePoint(new FileInfo(path)))
        {
            return false;
        }

        if (!IsAllowedExtension(Path.GetExtension(path)))
        {
            return false;
        }

        // Re-resolve each entry to close TOCTOU gaps between enumerate and use.
        var entryRelativePath = string.IsNullOrEmpty(relativePath)
            ? fileName
            : Path.Combine(relativePath, fileName);
        var entryResolution = TryResolveCandidatePath(rootAlias, entryRelativePath);
        return entryResolution.IsAllowed;
    }

    private FileSystemSandboxResolution ResolveAllowedReadFile(string candidateFullPath)
    {
        var extension = Path.GetExtension(candidateFullPath);
        if (!IsAllowedExtension(extension))
        {
            return FileSystemSandboxResolution.Denied("File extension is not allowed");
        }

        var fileInfo = new FileInfo(candidateFullPath);
        if (fileInfo.Length > _options.FileSystem.MaxReadBytes)
        {
            return FileSystemSandboxResolution.Denied("File exceeds maximum allowed size");
        }

        return FileSystemSandboxResolution.Allowed(candidateFullPath);
    }

    private FileSystemSandboxResolution TryResolveCandidatePath(string rootAlias, string relativePath)
    {
        if (!TryResolveRoot(rootAlias, out var root))
        {
            return FileSystemSandboxResolution.Denied("Unknown root alias");
        }

        if (ContainsAbsolutePathSyntax(relativePath))
        {
            return FileSystemSandboxResolution.Denied("Absolute paths are not allowed");
        }

        var normalizedRelativePath = relativePath.Replace('\\', '/');
        foreach (var segment in normalizedRelativePath.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (segment == "..")
            {
                return FileSystemSandboxResolution.Denied("Path traversal is not allowed");
            }
        }

        var candidateFullPath = Path.GetFullPath(Path.Combine(root.FullPath, relativePath));

        if (!IsPathWithinRoot(candidateFullPath, root.FullPath))
        {
            return FileSystemSandboxResolution.Denied("Path is outside the allowed root");
        }

        if (HasReparsePointInPath(candidateFullPath, root.FullPath))
        {
            return FileSystemSandboxResolution.Denied("Symbolic links are not allowed");
        }

        return FileSystemSandboxResolution.Allowed(candidateFullPath);
    }

    /// <summary>A configured sandbox root with its content-root-relative path fully resolved.</summary>
    private sealed record ResolvedRoot(string Alias, string FullPath);

    private bool TryResolveRoot(string rootAlias, out ResolvedRoot root)
    {
        if (!string.IsNullOrEmpty(rootAlias)
            && _options.FileSystem.Roots.TryGetValue(rootAlias, out var configuredRoot)
            && !string.IsNullOrWhiteSpace(configuredRoot.Path))
        {
            root = new ResolvedRoot(
                rootAlias,
                Path.GetFullPath(Path.Combine(_contentRoot, configuredRoot.Path)));
            return true;
        }

        root = null!;
        return false;
    }

    /// <summary>
    /// Copies the stream into memory as UTF-8 text, or returns null once the byte cap is exceeded.
    /// </summary>
    private static async Task<string?> ReadUpToMaxBytesAsync(
        FileStream stream,
        long maxBytes,
        CancellationToken cancellationToken)
    {
        using var memory = new MemoryStream();
        var buffer = new byte[8192];
        long totalRead = 0;

        while (true)
        {
            var bytesRead = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
            if (bytesRead == 0)
            {
                break;
            }

            totalRead += bytesRead;
            if (totalRead > maxBytes)
            {
                return null;
            }

            memory.Write(buffer, 0, bytesRead);
        }

        return Encoding.UTF8.GetString(memory.ToArray());
    }

    private static bool IsValidRootAlias(string? rootAlias, out string? errorMessage)
    {
        if (string.IsNullOrWhiteSpace(rootAlias))
        {
            errorMessage = "Root alias is required";
            return false;
        }

        if (rootAlias.Length > MaxRootAliasLength)
        {
            errorMessage = "Root alias is too long";
            return false;
        }

        errorMessage = null;
        return true;
    }

    internal static bool ContainsAbsolutePathSyntax(string relativePath) =>
        Path.IsPathRooted(relativePath) || relativePath.Contains(':', StringComparison.Ordinal);

    internal static ReadPathTargetKind ClassifyReadTarget(string fullPath)
    {
        if (File.Exists(fullPath))
        {
            return ReadPathTargetKind.File;
        }

        if (Directory.Exists(fullPath))
        {
            return ReadPathTargetKind.Directory;
        }

        return ReadPathTargetKind.Missing;
    }

    private static bool IsPathWithinRoot(string candidateFullPath, string rootFullPath)
    {
        var normalizedRoot = EnsureTrailingSeparator(Path.GetFullPath(rootFullPath));
        var normalizedCandidate = Path.GetFullPath(candidateFullPath);

        return normalizedCandidate.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase);
    }

    private static string EnsureTrailingSeparator(string path)
    {
        if (!path.EndsWith(Path.DirectorySeparatorChar))
        {
            return path + Path.DirectorySeparatorChar;
        }

        return path;
    }

    private static bool HasReparsePointInPath(string fullPath, string sandboxRootFullPath)
    {
        if (AnyAncestorHasReparsePoint(fullPath, sandboxRootFullPath))
        {
            return true;
        }

        return LeafHasReparsePoint(fullPath);
    }

    private static bool AnyAncestorHasReparsePoint(string fullPath, string sandboxRootFullPath)
    {
        var normalizedStop = Path.GetFullPath(sandboxRootFullPath)
            .TrimEnd(Path.DirectorySeparatorChar);
        var current = new DirectoryInfo(Path.GetDirectoryName(fullPath) ?? fullPath);

        while (true)
        {
            if (HasReparsePoint(current))
            {
                return true;
            }

            if (HasReachedSandboxBoundary(current, normalizedStop))
            {
                return false;
            }

            current = current.Parent!;
        }
    }

    private static bool HasReachedSandboxBoundary(DirectoryInfo current, string normalizedStop)
    {
        var currentFullName = current.FullName.TrimEnd(Path.DirectorySeparatorChar);
        if (string.Equals(currentFullName, normalizedStop, StringComparison.OrdinalIgnoreCase)
            || HasReachedFilesystemRoot(current))
        {
            return true;
        }

        // Do not walk above the sandbox root — ancestor junctions outside the root are irrelevant.
        return !currentFullName.StartsWith(
            EnsureTrailingSeparator(normalizedStop),
            StringComparison.OrdinalIgnoreCase);
    }

    private static bool LeafHasReparsePoint(string fullPath)
    {
        if (File.Exists(fullPath))
        {
            return HasReparsePoint(new FileInfo(fullPath));
        }

        if (Directory.Exists(fullPath))
        {
            return HasReparsePoint(new DirectoryInfo(fullPath));
        }

        return false;
    }

    private static bool HasReachedFilesystemRoot(DirectoryInfo current) =>
        current.Parent is null || string.Equals(
            current.FullName.TrimEnd(Path.DirectorySeparatorChar),
            current.Root.FullName.TrimEnd(Path.DirectorySeparatorChar),
            StringComparison.OrdinalIgnoreCase);

    private static bool HasReparsePoint(FileSystemInfo info) =>
        (info.Attributes & FileAttributes.ReparsePoint) != 0;
}

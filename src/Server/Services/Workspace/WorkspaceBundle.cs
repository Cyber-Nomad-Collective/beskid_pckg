using System.IO.Compression;

namespace Server.Services.Workspace;

public sealed class WorkspaceBundle
{
    private readonly IReadOnlyDictionary<string, byte[]> _entries;

    private WorkspaceBundle(IReadOnlyDictionary<string, byte[]> entries)
    {
        _entries = entries;
    }

    public static WorkspaceBundle FromZip(Stream zipStream)
    {
        using var memory = new MemoryStream();
        zipStream.CopyTo(memory);
        var bytes = memory.ToArray();
        if (bytes.Length == 0)
        {
            throw new InvalidOperationException("Workspace bundle is empty.");
        }

        using var archiveStream = new MemoryStream(bytes);
        using var zip = new ZipArchive(archiveStream, ZipArchiveMode.Read, leaveOpen: false);
        var entries = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in zip.Entries)
        {
            if (string.IsNullOrEmpty(entry.Name))
            {
                continue;
            }

            var path = WorkspaceManifestParsing.NormalizeZipEntryPath(entry.FullName);
            if (string.IsNullOrWhiteSpace(path))
            {
                continue;
            }

            using var entryStream = entry.Open();
            using var entryMemory = new MemoryStream();
            entryStream.CopyTo(entryMemory);
            entries[path] = entryMemory.ToArray();
        }

        if (entries.Count == 0)
        {
            throw new InvalidOperationException("Workspace bundle contains no files.");
        }

        return new WorkspaceBundle(entries);
    }

    public bool TryGetEntry(string relativePath, out byte[] content)
    {
        var normalized = WorkspaceManifestParsing.NormalizeRelativePath(relativePath);
        return _entries.TryGetValue(normalized, out content!);
    }

    public string RequireText(string relativePath)
    {
        if (!TryGetEntry(relativePath, out var bytes))
        {
            throw new InvalidOperationException($"Workspace bundle is missing '{relativePath}'.");
        }

        return System.Text.Encoding.UTF8.GetString(bytes);
    }

    public IReadOnlyList<string> ListMemberFiles(string memberRelativePath)
    {
        var prefix = WorkspaceManifestParsing.NormalizeRelativePath(memberRelativePath);
        if (prefix.Length > 0)
        {
            prefix += "/";
        }

        return _entries.Keys
            .Where(path => path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .Select(path => path[prefix.Length..])
            .Where(rel => !string.IsNullOrWhiteSpace(rel))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();
    }

    public IReadOnlyDictionary<string, byte[]> CollectMemberPackEntries(string memberRelativePath)
    {
        var prefix = WorkspaceManifestParsing.NormalizeRelativePath(memberRelativePath);
        if (prefix.Length > 0)
        {
            prefix += "/";
        }

        var result = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var (path, bytes) in _entries)
        {
            if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var relative = path[prefix.Length..];
            if (string.IsNullOrWhiteSpace(relative))
            {
                continue;
            }

            if (ShouldSkipMemberRelativePath(relative))
            {
                continue;
            }

            result[relative] = bytes;
        }

        if (!result.Keys.Any(key => key.StartsWith("src/", StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException(
                $"Workspace member '{memberRelativePath}' must include at least one file under src/.");
        }

        if (!result.ContainsKey("Project.proj"))
        {
            throw new InvalidOperationException(
                $"Workspace member '{memberRelativePath}' is missing Project.proj.");
        }

        return result;
    }

    private static bool ShouldSkipMemberRelativePath(string relativePath)
    {
        if (string.Equals(relativePath, "package.json", StringComparison.OrdinalIgnoreCase)
            || string.Equals(relativePath, "checksums.sha256", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var segments = relativePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segments.Any(segment =>
            string.Equals(segment, "obj", StringComparison.OrdinalIgnoreCase)
            || string.Equals(segment, "bin", StringComparison.OrdinalIgnoreCase)
            || string.Equals(segment, ".git", StringComparison.OrdinalIgnoreCase));
    }
}

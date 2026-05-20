using Server.Services.Artifacts;

namespace Server.Services;

/// <summary>
/// Resolves README markdown paths inside package artifacts (manifest hint, then defaults).
/// </summary>
public static class PackageReadmeResolver
{
    public static IReadOnlyList<string> CandidatePaths(string? manifestReadmePath)
    {
        var paths = new List<string>();
        TryAdd(paths, manifestReadmePath);
        TryAdd(paths, "README.md");
        return paths;
    }

    private static void TryAdd(List<string> paths, string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        var normalized = PackageZipPathNormalizer.Normalize(path);
        if (!PackageDocsPaths.IsListableMarkdownPath(normalized))
        {
            return;
        }

        if (paths.Any(existing => string.Equals(existing, normalized, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        paths.Add(normalized);
    }
}

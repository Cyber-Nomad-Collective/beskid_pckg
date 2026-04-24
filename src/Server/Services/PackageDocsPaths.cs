namespace Server.Services;

/// <summary>
/// Central allowlists for documentation paths inside package artifacts.
/// </summary>
public static class PackageDocsPaths
{
    /// <summary>
    /// Beskid CLI writes structured API documentation here (see <c>beskid doc</c> / pack).
    /// </summary>
    public const string StructuredApiDocRelativePath = ".beskid/docs/api.json";

    public static bool HasOnlySafePathSegments(string normalized)
    {
        foreach (var segment in normalized.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (segment is "." or "..")
            {
                return false;
            }
        }

        return true;
    }

    public static bool IsStructuredApiDocPath(string normalized)
    {
        if (!HasOnlySafePathSegments(normalized))
        {
            return false;
        }

        return string.Equals(normalized, StructuredApiDocRelativePath, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Markdown files shown in the documentation index (embedded + full-page sidebar).</summary>
    public static bool IsListableMarkdownPath(string normalized)
    {
        if (!HasOnlySafePathSegments(normalized))
        {
            return false;
        }

        if (string.Equals(normalized, "README.md", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!normalized.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (normalized.StartsWith("docs/", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return normalized.StartsWith(".beskid/docs/", StringComparison.OrdinalIgnoreCase);
    }
}

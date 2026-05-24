namespace Server.Services;

/// <summary>Shared rules for <c>api.json</c> paths (artifact-relative, same as <c>.bpk</c> zip entries).</summary>
public static class ApiDocArtifactPathValidation
{
    public static bool IsArtifactRelative(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var trimmed = path.Trim();
        if (Path.IsPathRooted(trimmed))
        {
            return false;
        }

        if (trimmed.StartsWith("/", StringComparison.Ordinal)
            || trimmed.StartsWith("\\", StringComparison.Ordinal))
        {
            return false;
        }

        if (trimmed.Length >= 2 && trimmed[1] == ':')
        {
            return false;
        }

        return PackageDocsPaths.HasOnlySafePathSegments(trimmed.Replace('\\', '/'));
    }
}

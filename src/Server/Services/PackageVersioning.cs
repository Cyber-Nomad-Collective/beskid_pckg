namespace Server.Services;

public enum RegistryVersionBump
{
    Patch,
    Minor,
    Major
}

/// <summary>
/// Semantic version core (major.minor.patch) for registry publish bumps and display ordering.
/// </summary>
public readonly record struct SemVerCore(int Major, int Minor, int Patch) : IComparable<SemVerCore>
{
    public int CompareTo(SemVerCore other)
    {
        var c = Major.CompareTo(other.Major);
        if (c != 0)
        {
            return c;
        }

        c = Minor.CompareTo(other.Minor);
        return c != 0 ? c : Patch.CompareTo(other.Patch);
    }

    public override string ToString() => $"{Major}.{Minor}.{Patch}";
}

public static class PackageVersioning
{
    public static RegistryVersionBump ParseBump(string? raw)
    {
        if (string.Equals(raw, "major", StringComparison.OrdinalIgnoreCase))
        {
            return RegistryVersionBump.Major;
        }

        if (string.Equals(raw, "minor", StringComparison.OrdinalIgnoreCase))
        {
            return RegistryVersionBump.Minor;
        }

        return RegistryVersionBump.Patch;
    }

    /// <summary>
    /// Parses X.Y.Z from the start of a semver string (ignores prerelease/build for ordering and bumps).
    /// </summary>
    public static bool TryParseCore(string? version, out SemVerCore core)
    {
        core = default;
        if (string.IsNullOrWhiteSpace(version))
        {
            return false;
        }

        var trimmed = version.Trim();
        var dash = trimmed.IndexOf('-');
        var plus = trimmed.IndexOf('+');
        var end = trimmed.Length;
        if (dash >= 0)
        {
            end = Math.Min(end, dash);
        }

        if (plus >= 0)
        {
            end = Math.Min(end, plus);
        }

        var corePart = trimmed[..end];
        var parts = corePart.Split('.', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 3)
        {
            return false;
        }

        if (!int.TryParse(parts[0], out var major)
            || !int.TryParse(parts[1], out var minor)
            || !int.TryParse(parts[2], out var patch))
        {
            return false;
        }

        if (major < 0 || minor < 0 || patch < 0)
        {
            return false;
        }

        core = new SemVerCore(major, minor, patch);
        return true;
    }

    /// <summary>
    /// Next version after the highest non-yanked core, using the given bump. If no prior versions, returns <c>0.0.1</c>.
    /// </summary>
    public static string ComputeNextVersion(IEnumerable<string> existingNonYankedVersions, RegistryVersionBump bump)
    {
        var max = new SemVerCore(0, 0, 0);
        var any = false;
        foreach (var v in existingNonYankedVersions)
        {
            if (!TryParseCore(v, out var c))
            {
                continue;
            }

            any = true;
            if (c.CompareTo(max) > 0)
            {
                max = c;
            }
        }

        if (!any)
        {
            return "0.0.1";
        }

        return bump switch
        {
            RegistryVersionBump.Major => new SemVerCore(max.Major + 1, 0, 0).ToString(),
            RegistryVersionBump.Minor => new SemVerCore(max.Major, max.Minor + 1, 0).ToString(),
            _ => new SemVerCore(max.Major, max.Minor, max.Patch + 1).ToString(),
        };
    }

    public static int CompareCore(string? a, string? b)
    {
        var okA = TryParseCore(a, out var ca);
        var okB = TryParseCore(b, out var cb);
        if (okA && okB)
        {
            return ca.CompareTo(cb);
        }

        if (okA)
        {
            return 1;
        }

        return okB ? -1 : string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Latest non-yanked version string by core semver ordering; falls back to lexicographic if unparsable.
    /// </summary>
    public static string? GetLatestNonYankedVersionString(IEnumerable<(string Version, bool IsYanked)> rows)
    {
        var list = rows.Where(r => !r.IsYanked).Select(r => r.Version).Where(v => !string.IsNullOrWhiteSpace(v)).ToList();
        if (list.Count == 0)
        {
            return null;
        }

        return list.OrderByDescending(v => v, Comparer<string>.Create((x, y) => CompareCore(x, y))).First();
    }
}

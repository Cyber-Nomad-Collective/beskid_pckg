using Microsoft.EntityFrameworkCore;
using Server.Data;
using Server.Services;

namespace Server.Features.Packages;

internal sealed record PackageEmbedPublicDto(
    string Name,
    string Description,
    string? LatestVersion,
    long TotalDownloads);

internal static class PackageEmbedQueries
{
    /// <summary>Public packages only — suitable for anonymous README and iframe embedding.</summary>
    public static async Task<PackageEmbedPublicDto?> TryGetPublicAsync(
        ApplicationDbContext db,
        string packageName,
        CancellationToken ct)
    {
        var trimmed = packageName.Trim();
        if (trimmed.Length == 0)
        {
            return null;
        }

        var package = await db.Packages.AsNoTracking()
            .SingleOrDefaultAsync(p => p.Name == trimmed, ct);
        if (package is null || !package.IsPublic)
        {
            return null;
        }

        var versionRows = await db.PackageVersions.AsNoTracking()
            .Where(v => v.PackageId == package.Id)
            .Select(v => new { v.Version, v.IsYanked })
            .ToListAsync(ct);

        var latest = PackageVersioning.GetLatestNonYankedVersionString(
            versionRows.Select(v => (v.Version, v.IsYanked)));

        return new PackageEmbedPublicDto(package.Name, package.Description, latest, package.TotalDownloads);
    }
}

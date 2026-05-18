using Microsoft.EntityFrameworkCore;
using Server.Data;
using Server.Features.Packages.Mapping;
using Server.Services;

namespace Server.Features.Packages;

public interface IPackageDetailsQuery
{
    Task<PackageDetailsResponse?> GetByIdOrNameAsync(
        HttpContext httpContext,
        string idOrName,
        CancellationToken cancellationToken = default);
}

public sealed class PackageDetailsQuery(
    ApplicationDbContext dbContext,
    IPackageAccessService packageAccess) : IPackageDetailsQuery
{
    public async Task<PackageDetailsResponse?> GetByIdOrNameAsync(
        HttpContext httpContext,
        string idOrName,
        CancellationToken cancellationToken = default)
    {
        var key = idOrName?.Trim();
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        var package = Guid.TryParse(key, out var packageId)
            ? await dbContext.Packages.AsNoTracking().SingleOrDefaultAsync(x => x.Id == packageId, cancellationToken)
            : await dbContext.Packages.AsNoTracking().SingleOrDefaultAsync(x => x.Name == key, cancellationToken);

        if (package is null || !await packageAccess.CanViewPackageAsync(httpContext, package, cancellationToken))
        {
            return null;
        }

        var packageVersions = await dbContext.PackageVersions
            .AsNoTracking()
            .Where(x => x.PackageId == package.Id)
            .ToListAsync(cancellationToken);
        packageVersions = packageVersions
            .OrderByDescending(x => x.PublishedAtUtc)
            .ToList();

        var tags = await dbContext.PackageTags
            .AsNoTracking()
            .Where(x => x.PackageId == package.Id)
            .Select(x => x.Tag)
            .OrderBy(x => x)
            .ToListAsync(cancellationToken);

        var pendingReviewCount = await dbContext.PackageReviews
            .AsNoTracking()
            .Where(x => x.PackageId == package.Id && x.Status == "Pending")
            .CountAsync(cancellationToken);

        var averageRating = await dbContext.PackageCommunityReviews
            .AsNoTracking()
            .Where(x => x.PackageId == package.Id)
            .Select(x => (double?)x.Rating)
            .AverageAsync(cancellationToken) ?? 0d;

        var reviewCount = await dbContext.PackageCommunityReviews
            .AsNoTracking()
            .Where(x => x.PackageId == package.Id)
            .CountAsync(cancellationToken);

        var avgDownloads = await dbContext.Packages
            .AsNoTracking()
            .Where(x => x.IsPublic)
            .AverageAsync(x => (double?)x.TotalDownloads, cancellationToken) ?? 0d;

        var health = PackageHealthScoring.Calculate(package, avgDownloads, averageRating, reviewCount);

        var latestManifest = packageVersions.FirstOrDefault()?.ManifestJson;
        var parsedManifest = PackageManifestMetadataReader.Read(latestManifest);
        var dependencies = parsedManifest.Dependencies
            .Select(d => new PackageDependencyResponse(d.Name, d.Version, d.Source, d.Registry))
            .ToList();

        var otherVersions = await dbContext.PackageVersions
            .AsNoTracking()
            .Where(x => x.PackageId != package.Id)
            .Select(x => new { x.PackageId, x.ManifestJson })
            .ToListAsync(cancellationToken);

        var otherLatestManifests = otherVersions
            .GroupBy(x => x.PackageId)
            .Select(group => group.FirstOrDefault()?.ManifestJson)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();

        var dependentsCount = otherLatestManifests.Count(manifest =>
            PackageManifestMetadataReader.Read(manifest).Dependencies.Any(d =>
                string.Equals(d.Name, package.Name, StringComparison.OrdinalIgnoreCase)));

        DateTimeOffset? firstPublishedAtUtc = packageVersions.Count > 0
            ? packageVersions.Min(x => x.PublishedAtUtc)
            : null;
        var activeVersions = packageVersions.Where(x => !x.IsYanked).ToList();
        DateTimeOffset? lastPublishedAtUtc = activeVersions.Count > 0
            ? activeVersions.Max(x => x.PublishedAtUtc)
            : null;
        if (lastPublishedAtUtc is null && packageVersions.Count > 0)
        {
            lastPublishedAtUtc = packageVersions.Max(x => x.PublishedAtUtc);
        }

        var latestVersion = PackageVersioning.GetLatestNonYankedVersionString(
            packageVersions.Select(x => (x.Version, x.IsYanked)));

        var ownerMap = await dbContext.GetPublisherRowsAsync(new[] { package.OwnerUserId }, cancellationToken);
        var ownerRow = ownerMap.TryGetValue(package.OwnerUserId, out var o)
            ? o
            : new PublisherOwnerRow(string.Empty, false);

        return new PackageDetailsResponse(
            PackageResponseMapper.ToSummary(
                package,
                tags,
                pendingReviewCount,
                averageRating,
                ownerRow),
            packageVersions
                .Select(x => PackageResponseMapper.ToVersionSummary(x, package.Name))
                .ToList(),
            dependencies,
            dependentsCount,
            parsedManifest.Readme,
            PackageResponseMapper.ToHealthSnapshot(health),
            firstPublishedAtUtc,
            lastPublishedAtUtc,
            latestVersion);
    }
}

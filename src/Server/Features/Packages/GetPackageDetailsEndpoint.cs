using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using Server.Data;
using Server.Services;

namespace Server.Features.Packages;

public sealed class GetPackageDetailsEndpoint(
    ApplicationDbContext dbContext,
    IApiPrincipalResolver principalResolver)
    : EndpointWithoutRequest<PackageDetailsResponse>
{
    public override void Configure()
    {
        Get("/packages/{IdOrName}");
        AllowAnonymous();
        Summary(s => s.Summary = "Get package details by id or name.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var idOrName = Route<string>("IdOrName")?.Trim();
        if (string.IsNullOrWhiteSpace(idOrName))
        {
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await Send.NotFoundAsync(ct);
            return;
        }

        var package = Guid.TryParse(idOrName, out var packageId)
            ? await dbContext.Packages.AsNoTracking().SingleOrDefaultAsync(x => x.Id == packageId, ct)
            : await dbContext.Packages.AsNoTracking().SingleOrDefaultAsync(x => x.Name == idOrName, ct);
        if (package is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        if (!package.IsPublic)
        {
            var userId = await principalResolver.ResolveUserIdAsync(HttpContext, ct);
            var isSuperAdmin = User.IsInRole("SuperAdmin");
            if (string.IsNullOrWhiteSpace(userId) || (!isSuperAdmin && userId != package.OwnerUserId))
            {
                HttpContext.Response.StatusCode = StatusCodes.Status403Forbidden;
                await Send.NotFoundAsync(ct);
                return;
            }
        }

        var packageVersions = await dbContext.PackageVersions
            .AsNoTracking()
            .Where(x => x.PackageId == package.Id)
            .ToListAsync(ct);
        packageVersions = packageVersions
            .OrderByDescending(x => x.PublishedAtUtc)
            .ToList();

        var tags = await dbContext.PackageTags
            .AsNoTracking()
            .Where(x => x.PackageId == package.Id)
            .Select(x => x.Tag)
            .OrderBy(x => x)
            .ToListAsync(ct);

        var pendingReviewCount = await dbContext.PackageReviews
            .AsNoTracking()
            .Where(x => x.PackageId == package.Id && x.Status == "Pending")
            .CountAsync(ct);

        var averageRating = await dbContext.PackageCommunityReviews
            .AsNoTracking()
            .Where(x => x.PackageId == package.Id)
            .Select(x => (double?)x.Rating)
            .AverageAsync(ct) ?? 0d;

        var reviewCount = await dbContext.PackageCommunityReviews
            .AsNoTracking()
            .Where(x => x.PackageId == package.Id)
            .CountAsync(ct);

        var avgDownloads = await dbContext.Packages
            .AsNoTracking()
            .Where(x => x.IsPublic)
            .AverageAsync(x => (double?)x.TotalDownloads) ?? 0d;

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
            .ToListAsync(ct);

        var otherLatestManifests = otherVersions
            .GroupBy(x => x.PackageId)
            .Select(group => group.FirstOrDefault()?.ManifestJson)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();

        var dependentsCount = otherLatestManifests.Count(manifest =>
            PackageManifestMetadataReader.Read(manifest).Dependencies.Any(d =>
                string.Equals(d.Name, package.Name, StringComparison.OrdinalIgnoreCase)));

        var response = new PackageDetailsResponse(
            new PackageSummaryResponse(
                package.Id,
                package.Name,
                package.Description,
                package.Category,
                package.RepositoryUrl,
                package.WebsiteUrl,
                tags,
                package.IsPublic,
                package.TotalDownloads,
                package.UpdatedAtUtc,
                pendingReviewCount,
                Math.Round(averageRating, 2)),
            packageVersions
                .Select(x => new PackageVersionSummaryResponse(
                    x.Id,
                    x.PackageId,
                    package.Name,
                    x.Version,
                    x.IsYanked,
                    x.ChecksumSha256,
                    x.SizeBytes,
                    x.PublishedAtUtc,
                    x.YankedAtUtc))
                .ToList(),
            dependencies,
            dependentsCount,
            parsedManifest.Readme,
            ToHealth(health));

        await Send.OkAsync(response, ct);
    }

    private static PackageHealthSnapshotResponse ToHealth(PackageHealthStatus health)
        => new(
            health.State,
            health.SubState,
            health.Score,
            health.UpdateRate.State,
            health.UpdateRate.SubState,
            health.UpdateRate.Normalized,
            health.UpdateRate.Weight,
            health.Downloads.State,
            health.Downloads.SubState,
            health.Downloads.Normalized,
            health.Downloads.Weight,
            health.Reviews.State,
            health.Reviews.SubState,
            health.Reviews.Normalized,
            health.Reviews.Weight);
}

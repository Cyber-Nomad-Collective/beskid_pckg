using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using Server.Data;
using Server.Services;

namespace Server.Features.Packages;

public sealed class GetPackagesEndpoint(
    ApplicationDbContext dbContext,
    IApiPrincipalResolver principalResolver)
    : EndpointWithoutRequest<List<PackageSummaryResponse>>
{
    public override void Configure()
    {
        Get("/packages");
        AllowAnonymous();
        Summary(s =>
        {
            s.Summary = "List public packages and caller-owned private packages.";
        });
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var userId = await principalResolver.ResolveUserIdAsync(HttpContext, ct);
        var isSuperAdmin = User.IsInRole("SuperAdmin");

        var packagesQuery = dbContext.Packages
            .AsNoTracking()
            .Where(x => isSuperAdmin || x.IsPublic || (!string.IsNullOrWhiteSpace(userId) && x.OwnerUserId == userId));

        var packages = await packagesQuery.ToListAsync(ct);

        packages = packages
            .OrderByDescending(x => x.UpdatedAtUtc)
            .ToList();

        var packageIds = packages.Select(x => x.Id).ToList();

        var pendingCounts = await dbContext.PackageReviews
            .AsNoTracking()
            .Where(x => packageIds.Contains(x.PackageId) && x.Status == "Pending")
            .GroupBy(x => x.PackageId)
            .Select(group => new { group.Key, Count = group.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);

        var ratingAverages = await dbContext.PackageCommunityReviews
            .AsNoTracking()
            .Where(x => packageIds.Contains(x.PackageId))
            .GroupBy(x => x.PackageId)
            .Select(group => new { group.Key, Average = group.Average(x => x.Rating) })
            .ToDictionaryAsync(x => x.Key, x => x.Average, ct);

        var tagsByPackageId = await dbContext.PackageTags
            .AsNoTracking()
            .Where(x => packageIds.Contains(x.PackageId))
            .GroupBy(x => x.PackageId)
            .Select(group => new { group.Key, Tags = group.Select(x => x.Tag).OrderBy(tag => tag).ToList() })
            .ToDictionaryAsync(x => x.Key, x => (IReadOnlyList<string>)x.Tags, ct);

        var response = packages.Select(x => new PackageSummaryResponse(
                x.Id,
                x.Name,
                x.Description,
                x.Category,
                x.RepositoryUrl,
                x.WebsiteUrl,
                tagsByPackageId.GetValueOrDefault(x.Id) ?? [],
                x.IsPublic,
                x.TotalDownloads,
                x.UpdatedAtUtc,
                pendingCounts.GetValueOrDefault(x.Id),
                Math.Round(ratingAverages.GetValueOrDefault(x.Id), 2)))
            .ToList();

        await Send.OkAsync(response, ct);
    }
}

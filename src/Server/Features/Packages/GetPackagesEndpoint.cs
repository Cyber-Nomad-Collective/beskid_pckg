using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using Server.Data;
using Server.Features.Packages.Mapping;
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

        var owners = await dbContext.GetPublisherRowsAsync(packages.Select(p => p.OwnerUserId), ct);

        var response = packages.Select(x =>
            {
                var owner = owners.TryGetValue(x.OwnerUserId, out var row)
                    ? row
                    : new PublisherOwnerRow(string.Empty, false);
                return PackageResponseMapper.ToSummary(
                    x,
                    tagsByPackageId.GetValueOrDefault(x.Id) ?? [],
                    pendingCounts.GetValueOrDefault(x.Id),
                    ratingAverages.GetValueOrDefault(x.Id),
                    owner);
            })
            .ToList();

        await Send.OkAsync(response, ct);
    }
}

using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using Server.Data;
using Server.Services;

namespace Server.Features.Packages;

public sealed class SearchPackagesEndpoint(
    ApplicationDbContext dbContext,
    IApiPrincipalResolver principalResolver)
    : EndpointWithoutRequest<List<PackageSearchResponse>>
{
    public override void Configure()
    {
        Get("/search");
        Options(x => x.RequireRateLimiting("search"));
        AllowAnonymous();
        Summary(s => s.Summary = "Search packages with filters.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var query = HttpContext.Request.Query;
        var q = query["q"].FirstOrDefault()?.Trim() ?? string.Empty;
        var tag = query["tag"].FirstOrDefault()?.Trim() ?? "all";
        var topic = query["topic"].FirstOrDefault()?.Trim() ?? "all";
        var status = query["status"].FirstOrDefault()?.Trim() ?? "all";
        var sort = query["sort"].FirstOrDefault()?.Trim() ?? "popularity";
        var descending = !string.Equals(query["order"].FirstOrDefault(), "asc", StringComparison.OrdinalIgnoreCase);
        var minReviews = int.TryParse(query["minReviews"].FirstOrDefault(), out var parsedMinReviews) ? Math.Max(parsedMinReviews, 0) : 0;
        var limit = int.TryParse(query["limit"].FirstOrDefault(), out var parsedLimit) ? Math.Clamp(parsedLimit, 1, 200) : 100;

        var userId = await principalResolver.ResolveUserIdAsync(HttpContext, ct);
        var isSuperAdmin = User.IsInRole("SuperAdmin");

        var packagesQuery = dbContext.Packages
            .AsNoTracking()
            .Where(x => isSuperAdmin || x.IsPublic || (!string.IsNullOrWhiteSpace(userId) && x.OwnerUserId == userId));

        if (!string.IsNullOrWhiteSpace(q))
        {
            packagesQuery = packagesQuery.Where(x =>
                x.Name.Contains(q) || x.Category.Contains(q) || x.Description.Contains(q));
        }

        var packages = await packagesQuery.ToListAsync(ct);
        var packageIds = packages.Select(x => x.Id).ToList();

        var pendingCounts = await dbContext.PackageReviews
            .AsNoTracking()
            .Where(x => packageIds.Contains(x.PackageId) && x.Status == "Pending")
            .GroupBy(x => x.PackageId)
            .Select(group => new { group.Key, Count = group.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);

        var ratings = await dbContext.PackageCommunityReviews
            .AsNoTracking()
            .Where(x => packageIds.Contains(x.PackageId))
            .GroupBy(x => x.PackageId)
            .Select(group => new
            {
                group.Key,
                Average = group.Average(x => x.Rating),
                Count = group.Count()
            })
            .ToListAsync(ct);
        var avgRatingById = ratings.ToDictionary(x => x.Key, x => x.Average);
        var reviewCountById = ratings.ToDictionary(x => x.Key, x => x.Count);

        var tagsByPackageId = await dbContext.PackageTags
            .AsNoTracking()
            .Where(x => packageIds.Contains(x.PackageId))
            .GroupBy(x => x.PackageId)
            .Select(group => new { group.Key, Tags = group.Select(x => x.Tag).ToList() })
            .ToDictionaryAsync(x => x.Key, x => (IReadOnlyList<string>)x.Tags, ct);

        var avgDownloads = packages.Count == 0 ? 0d : packages.Average(x => (double)x.TotalDownloads);

        var rows = packages.Select(x =>
        {
            var avgRating = avgRatingById.GetValueOrDefault(x.Id);
            var reviewCount = reviewCountById.GetValueOrDefault(x.Id);
            var health = PackageHealthScoring.Calculate(x, avgDownloads, avgRating, reviewCount);
            var tags = tagsByPackageId.GetValueOrDefault(x.Id) ?? [];
            return new PackageSearchResponse(
                new PackageSummaryResponse(
                    x.Id,
                    x.Name,
                    x.Description,
                    x.Category,
                    x.RepositoryUrl,
                    x.WebsiteUrl,
                    tags,
                    x.IsPublic,
                    x.TotalDownloads,
                    x.UpdatedAtUtc,
                    pendingCounts.GetValueOrDefault(x.Id),
                    Math.Round(avgRating, 2),
                    x.IconUrl),
                reviewCount,
                new PackageHealthSnapshotResponse(
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
                    health.Reviews.Weight));
        });

        if (!string.Equals(tag, "all", StringComparison.OrdinalIgnoreCase))
        {
            rows = rows.Where(x => x.Package.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase));
        }

        if (!string.Equals(topic, "all", StringComparison.OrdinalIgnoreCase))
        {
            rows = rows.Where(x => string.Equals(x.Package.Category, topic, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.Equals(status, "all", StringComparison.OrdinalIgnoreCase))
        {
            rows = rows.Where(x => string.Equals(x.Health.State, status, StringComparison.OrdinalIgnoreCase));
        }

        if (minReviews > 0)
        {
            rows = rows.Where(x => x.ReviewCount >= minReviews);
        }

        rows = sort switch
        {
            "updated" => descending ? rows.OrderByDescending(x => x.Package.UpdatedAtUtc) : rows.OrderBy(x => x.Package.UpdatedAtUtc),
            "reviews" => descending ? rows.OrderByDescending(x => x.ReviewCount) : rows.OrderBy(x => x.ReviewCount),
            "status" => descending ? rows.OrderByDescending(x => x.Health.Score) : rows.OrderBy(x => x.Health.Score),
            _ => descending ? rows.OrderByDescending(x => x.Package.TotalDownloads) : rows.OrderBy(x => x.Package.TotalDownloads),
        };

        await Send.OkAsync(rows.Take(limit).ToList(), ct);
    }
}

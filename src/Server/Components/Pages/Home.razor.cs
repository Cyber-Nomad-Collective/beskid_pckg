using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Server.Components.Pages;

public partial class Home
{
    private const string HomeSnapshotCacheKey = "home-dashboard-snapshot";
    private int TotalPackages;
    private long TotalDownloads;
    private int TotalPublishers;
    private int PendingReviews;
    private readonly List<CategorySummary> TopCategories = [];
    private readonly List<TrendingSummary> TrendingPackages = [];

    protected override async Task OnInitializedAsync()
    {
        var snapshot = await Cache.GetOrCreateAsync(HomeSnapshotCacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(20);

            var totalPackages = await DbContext.Packages.CountAsync();
            var totalDownloads = await DbContext.Packages.SumAsync(x => (long?)x.TotalDownloads) ?? 0L;
            var totalPublishers = await DbContext.Packages.Select(x => x.OwnerUserId).Distinct().CountAsync();
            var pendingReviews = await DbContext.PackageReviews.CountAsync(x => x.Status == "Pending");

            var topCategories = await DbContext.Packages
                .AsNoTracking()
                .GroupBy(x => x.Category)
                .OrderByDescending(g => g.Count())
                .Take(5)
                .Select(g => new CategorySummary(g.Key, g.Count()))
                .ToListAsync();

            var trendingRows = await DbContext.Packages
                .AsNoTracking()
                .Where(x => x.IsPublic)
                .Select(x => new TrendingSummary(x.Name, x.TotalDownloads))
                .ToListAsync();

            return new HomeSnapshot(
                totalPackages,
                totalDownloads,
                totalPublishers,
                pendingReviews,
                topCategories,
                trendingRows.OrderByDescending(x => x.TotalDownloads).Take(8).ToList());
        });

        if (snapshot is null)
        {
            return;
        }

        TotalPackages = snapshot.TotalPackages;
        TotalDownloads = snapshot.TotalDownloads;
        TotalPublishers = snapshot.TotalPublishers;
        PendingReviews = snapshot.PendingReviews;

        TopCategories.Clear();
        TopCategories.AddRange(snapshot.TopCategories);

        TrendingPackages.Clear();
        TrendingPackages.AddRange(snapshot.TrendingPackages);
    }

    private sealed record HomeSnapshot(
        int TotalPackages,
        long TotalDownloads,
        int TotalPublishers,
        int PendingReviews,
        List<CategorySummary> TopCategories,
        List<TrendingSummary> TrendingPackages);

    private sealed record CategorySummary(string Category, int Count);

    private sealed record TrendingSummary(string Name, long TotalDownloads);
}
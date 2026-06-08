using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Server.Components.Shared;
using Server.Data;

namespace Server.Components.Pages;

public partial class Home
{
    private const string HomeSnapshotCacheKey = "home-dashboard-snapshot-v2";

    private int TotalPackages;
    private long TotalDownloads;
    private int TotalPublishers;
    private int PendingReviews;
    private int PublicPackageCount;
    private int PackagesWithReviews;
    private readonly List<HomeCategoryRow> TopCategories = [];
    private readonly List<PackageCarouselSlide> CarouselSlides = [];
    private readonly List<HomeCommunityPostRow> RecentPosts = [];
    private readonly List<HomeReviewRow> RecentReviews = [];

    protected override async Task OnInitializedAsync()
    {
        var snapshot = await Cache.GetOrCreateAsync(HomeSnapshotCacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(20);

            var totalPackages = await DbContext.Packages.CountAsync();
            var publicPackages = await DbContext.Packages.AsNoTracking().Where(x => x.IsPublic).ToListAsync();
            var publicPackageCount = publicPackages.Count;
            var totalDownloads = await DbContext.Packages.SumAsync(x => (long?)x.TotalDownloads) ?? 0L;
            var totalPublishers = await DbContext.Packages.Select(x => x.OwnerUserId).Distinct().CountAsync();
            var pendingReviews = await DbContext.PackageReviews.CountAsync(x => x.Status == "Pending");

            var ratings = await DbContext.PackageCommunityReviews
                .AsNoTracking()
                .GroupBy(x => x.PackageId)
                .Select(group => new
                {
                    group.Key,
                    Average = group.Average(x => x.Rating),
                    Count = group.Count(),
                })
                .ToListAsync();
            var avgRatingById = ratings.ToDictionary(x => x.Key, x => x.Average);
            var reviewCountById = ratings.ToDictionary(x => x.Key, x => x.Count);
            var packagesWithReviews = ratings.Count;

            static PackageListRow ToRow(
                PackageEntity pkg,
                IReadOnlyDictionary<Guid, double> avgById,
                IReadOnlyDictionary<Guid, int> countById) =>
                new(
                    pkg.Name,
                    pkg.Description,
                    pkg.IconUrl,
                    pkg.Category,
                    pkg.TotalDownloads,
                    avgById.TryGetValue(pkg.Id, out var avg) ? avg : 0d,
                    countById.TryGetValue(pkg.Id, out var count) ? count : 0,
                    pkg.UpdatedAtUtc);

            var trending = publicPackages
                .OrderByDescending(x => x.TotalDownloads)
                .ThenBy(x => x.Name, StringComparer.Ordinal)
                .Take(6)
                .Select(x => ToRow(x, avgRatingById, reviewCountById))
                .ToList();

            var recentlyUpdated = publicPackages
                .OrderByDescending(x => x.UpdatedAtUtc)
                .ThenBy(x => x.Name, StringComparer.Ordinal)
                .Take(6)
                .Select(x => ToRow(x, avgRatingById, reviewCountById))
                .ToList();

            var topRated = publicPackages
                .Where(x => reviewCountById.TryGetValue(x.Id, out var c) && c > 0)
                .OrderByDescending(x => avgRatingById[x.Id])
                .ThenByDescending(x => reviewCountById[x.Id])
                .ThenBy(x => x.Name, StringComparer.Ordinal)
                .Take(6)
                .Select(x => ToRow(x, avgRatingById, reviewCountById))
                .ToList();

            var carouselSlides = new List<PackageCarouselSlide>
            {
                new("trending", "Trending", "Most downloaded public packages.", trending),
                new("updated", "Recently updated", "Packages with fresh releases.", recentlyUpdated),
                new("rated", "Top rated", "Highest community-rated packages.", topRated),
            };

            var topCategoryRows = await DbContext.Packages
                .AsNoTracking()
                .Where(x => x.IsPublic)
                .GroupBy(x => x.Category)
                .OrderByDescending(g => g.Count())
                .Take(5)
                .Select(g => new { Category = g.Key, Count = g.Count() })
                .ToListAsync();

            var topCategories = topCategoryRows
                .Select(row =>
                {
                    var topPackage = publicPackages
                        .Where(x => x.Category == row.Category)
                        .OrderByDescending(x => x.TotalDownloads)
                        .Select(x => x.Name)
                        .FirstOrDefault() ?? string.Empty;
                    return new HomeCategoryRow(row.Category, row.Count, topPackage);
                })
                .ToList();

            var recentPosts = await DbContext.BoardPosts
                .AsNoTracking()
                .Where(x => !x.IsDeleted)
                .OrderByDescending(x => x.CreatedAtUtc)
                .Take(5)
                .Join(
                    DbContext.Boards.AsNoTracking(),
                    post => post.BoardId,
                    board => board.Id,
                    (post, board) => new { post, board })
                .ToListAsync();

            var packageIdsOnBoards = recentPosts
                .Where(x => string.Equals(x.board.EntityType, "Package", StringComparison.OrdinalIgnoreCase)
                    && Guid.TryParse(x.board.EntityId, out _))
                .Select(x => Guid.Parse(x.board.EntityId!))
                .Distinct()
                .ToList();
            var packageNamesById = await DbContext.Packages
                .AsNoTracking()
                .Where(x => packageIdsOnBoards.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, x => x.Name);

            var postAuthorIds = recentPosts.Select(x => x.post.AuthorUserId).Distinct().ToList();
            var postAuthors = await DbContext.Users
                .AsNoTracking()
                .Where(u => postAuthorIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.DisplayName);

            var communityPosts = recentPosts
                .Select(row =>
                {
                    var net = row.post.UpvoteCount - row.post.DownvoteCount;
                    string? packageName = null;
                    if (string.Equals(row.board.EntityType, "Package", StringComparison.OrdinalIgnoreCase)
                        && Guid.TryParse(row.board.EntityId, out var packageId)
                        && packageNamesById.TryGetValue(packageId, out var resolvedName))
                    {
                        packageName = resolvedName;
                    }
                    postAuthors.TryGetValue(row.post.AuthorUserId, out var author);
                    return new HomeCommunityPostRow(
                        row.post.Id,
                        row.post.Title,
                        row.board.Name,
                        packageName,
                        string.IsNullOrWhiteSpace(author) ? "Community member" : author,
                        row.post.CreatedAtUtc,
                        net);
                })
                .ToList();

            var reviewRows = await DbContext.PackageCommunityReviews
                .AsNoTracking()
                .OrderByDescending(x => x.CreatedAtUtc)
                .Take(5)
                .Join(
                    DbContext.Packages.AsNoTracking(),
                    review => review.PackageId,
                    package => package.Id,
                    (review, package) => new { review, package })
                .ToListAsync();

            var reviewAuthorIds = reviewRows.Select(x => x.review.UserId).Distinct().ToList();
            var reviewAuthors = await DbContext.Users
                .AsNoTracking()
                .Where(u => reviewAuthorIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.DisplayName);

            var recentReviews = reviewRows
                .Select(row =>
                {
                    reviewAuthors.TryGetValue(row.review.UserId, out var author);
                    var preview = row.review.Comment.Trim();
                    if (preview.Length > 120)
                    {
                        preview = $"{preview[..117]}…";
                    }

                    return new HomeReviewRow(
                        row.review.Id,
                        row.package.Name,
                        row.package.IconUrl,
                        string.IsNullOrWhiteSpace(author) ? "Community member" : author,
                        row.review.Rating,
                        preview,
                        row.review.CreatedAtUtc);
                })
                .ToList();

            return new HomeSnapshot(
                totalPackages,
                totalDownloads,
                totalPublishers,
                pendingReviews,
                publicPackageCount,
                packagesWithReviews,
                topCategories,
                carouselSlides,
                communityPosts,
                recentReviews);
        });

        if (snapshot is null)
        {
            return;
        }

        TotalPackages = snapshot.TotalPackages;
        TotalDownloads = snapshot.TotalDownloads;
        TotalPublishers = snapshot.TotalPublishers;
        PendingReviews = snapshot.PendingReviews;
        PublicPackageCount = snapshot.PublicPackageCount;
        PackagesWithReviews = snapshot.PackagesWithReviews;

        TopCategories.Clear();
        TopCategories.AddRange(snapshot.TopCategories);

        CarouselSlides.Clear();
        CarouselSlides.AddRange(snapshot.CarouselSlides);

        RecentPosts.Clear();
        RecentPosts.AddRange(snapshot.RecentPosts);

        RecentReviews.Clear();
        RecentReviews.AddRange(snapshot.RecentReviews);
    }

    private sealed record HomeSnapshot(
        int TotalPackages,
        long TotalDownloads,
        int TotalPublishers,
        int PendingReviews,
        int PublicPackageCount,
        int PackagesWithReviews,
        List<HomeCategoryRow> TopCategories,
        List<PackageCarouselSlide> CarouselSlides,
        List<HomeCommunityPostRow> RecentPosts,
        List<HomeReviewRow> RecentReviews);
}

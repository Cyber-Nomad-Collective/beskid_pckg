using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using Server.Data;

namespace Server.Components.Pages;

public partial class Packages
{
    [Inject] private ApplicationDbContext DbContext { get; set; } = default!;

    private readonly List<PackageBrowseRow> Rows = [];
    private readonly List<string> AvailableTags = [];
    private readonly List<string> AvailableTopics = [];
    private string Search = string.Empty;
    private string SelectedTag = "all";
    private string SelectedTopic = "all";
    private string SelectedStatus = "all";
    private string SelectedSort = "popularity";
    private int MinReviews;
    private bool SortDescending = true;

    protected override async Task OnInitializedAsync() => await LoadAsync();

    private async Task LoadAsync()
    {
        var query = DbContext.Packages.AsNoTracking().Where(x => x.IsPublic);
        if (!string.IsNullOrWhiteSpace(Search))
        {
            var needle = Search.Trim();
            query = query.Where(x =>
                x.Name.Contains(needle) || x.Category.Contains(needle) || x.Description.Contains(needle));
        }

        var basePackages = await query.ToListAsync();
        var packageIds = basePackages.Select(x => x.Id).ToList();

        var pendingCounts = await DbContext.PackageReviews
            .AsNoTracking()
            .Where(x => packageIds.Contains(x.PackageId) && x.Status == "Pending")
            .GroupBy(x => x.PackageId)
            .Select(group => new { group.Key, Count = group.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count);

        var ratingAverages = await DbContext.PackageCommunityReviews
            .AsNoTracking()
            .Where(x => packageIds.Contains(x.PackageId))
            .GroupBy(x => x.PackageId)
            .Select(group => new { group.Key, Average = group.Average(x => x.Rating) })
            .ToDictionaryAsync(x => x.Key, x => x.Average);

        var reviewCounts = await DbContext.PackageCommunityReviews
            .AsNoTracking()
            .Where(x => packageIds.Contains(x.PackageId))
            .GroupBy(x => x.PackageId)
            .Select(group => new { group.Key, Count = group.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count);

        var tagLookup = await DbContext.PackageTags
            .AsNoTracking()
            .Where(x => packageIds.Contains(x.PackageId))
            .GroupBy(x => x.PackageId)
            .Select(group => new { group.Key, Tags = group.Select(x => x.Tag).ToList() })
            .ToDictionaryAsync(x => x.Key, x => x.Tags);

        var topics = await DbContext.Topics
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .Select(x => x.Name)
            .ToListAsync();

        AvailableTopics.Clear();
        AvailableTopics.AddRange(topics.Distinct(StringComparer.OrdinalIgnoreCase));

        var avgDownloads = basePackages.Count == 0
            ? 0d
            : basePackages.Average(x => (double)x.TotalDownloads);

        var packageRows = basePackages
            .Select(x =>
            {
                var rawTags = tagLookup.GetValueOrDefault(x.Id) ?? new List<string>();
                var normalizedTags = rawTags
                    .Append(x.Category)
                    .Where(t => !string.IsNullOrWhiteSpace(t))
                    .Select(t => t.Trim().ToLowerInvariant())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                var status = BuildStatus(x, avgDownloads, ratingAverages.GetValueOrDefault(x.Id), reviewCounts.GetValueOrDefault(x.Id));
                return new PackageBrowseRow(
                x.Id,
                x.Name,
                x.Description,
                x.Category,
                x.RepositoryUrl,
                x.WebsiteUrl,
                x.IsPublic,
                x.TotalDownloads,
                x.UpdatedAtUtc,
                pendingCounts.GetValueOrDefault(x.Id),
                Math.Round(ratingAverages.GetValueOrDefault(x.Id), 2),
                reviewCounts.GetValueOrDefault(x.Id),
                normalizedTags,
                status)
                {
                    Topic = x.Category
                };
            })
            .ToList();

        AvailableTags.Clear();
        AvailableTags.AddRange(packageRows
            .SelectMany(x => x.Tags)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x));

        IEnumerable<PackageBrowseRow> filtered = packageRows;
        if (!string.Equals(SelectedTag, "all", StringComparison.OrdinalIgnoreCase))
        {
            filtered = filtered.Where(x => x.Tags.Contains(SelectedTag, StringComparer.OrdinalIgnoreCase));
        }

        if (!string.Equals(SelectedTopic, "all", StringComparison.OrdinalIgnoreCase))
        {
            filtered = filtered.Where(x => string.Equals(x.Topic, SelectedTopic, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.Equals(SelectedStatus, "all", StringComparison.OrdinalIgnoreCase))
        {
            filtered = filtered.Where(x => string.Equals(x.Status.State, SelectedStatus, StringComparison.OrdinalIgnoreCase));
        }

        if (MinReviews > 0)
        {
            filtered = filtered.Where(x => x.ReviewCount >= MinReviews);
        }

        filtered = SelectedSort switch
        {
            "updated" => SortDescending ? filtered.OrderByDescending(x => x.UpdatedAtUtc) : filtered.OrderBy(x => x.UpdatedAtUtc),
            "reviews" => SortDescending ? filtered.OrderByDescending(x => x.ReviewCount) : filtered.OrderBy(x => x.ReviewCount),
            "status" => SortDescending ? filtered.OrderByDescending(x => x.Status.Score) : filtered.OrderBy(x => x.Status.Score),
            _ => SortDescending ? filtered.OrderByDescending(x => x.TotalDownloads) : filtered.OrderBy(x => x.TotalDownloads)
        };

        Rows.Clear();
        Rows.AddRange(filtered.Take(100));
    }

    private static PackageHealthStatus BuildStatus(PackageEntity package, double averageDownloads, double averageRating, int reviewCount)
    {
        var now = DateTimeOffset.UtcNow;
        var daysSinceUpdate = Math.Max(0d, (now - package.UpdatedAtUtc).TotalDays);
        var downloadRatio = averageDownloads <= 0 ? 1d : package.TotalDownloads / averageDownloads;

        var updateState = BuildUpdateRateState(daysSinceUpdate);
        var downloadState = BuildDownloadState(downloadRatio);
        var reviewState = BuildReviewState(averageRating, reviewCount);

        var score = StatusScoreBuilder
            .Create()
            .Add(updateState.Weight, updateState.Normalized)
            .Add(downloadState.Weight, downloadState.Normalized)
            .Add(reviewState.Weight, reviewState.Normalized)
            .Build();

        var overall = score switch
        {
            >= 0.85 => ("thriving", "outstanding"),
            >= 0.68 => ("rising", "strong"),
            >= 0.48 => ("steady", "maintained"),
            _ => ("at-risk", "watchlist")
        };

        return new PackageHealthStatus(overall.Item1, overall.Item2, score, updateState, downloadState, reviewState);
    }

    private static FactorStatus BuildUpdateRateState(double daysSinceUpdate) => daysSinceUpdate switch
    {
        <= 2 => new FactorStatus("update-rate", "fast-track", "blazing", 1d, 0.42),
        <= 7 => new FactorStatus("update-rate", "fast-track", "surging", 0.93, 0.42),
        <= 14 => new FactorStatus("update-rate", "active", "rapid", 0.82, 0.42),
        <= 30 => new FactorStatus("update-rate", "active", "warm", 0.72, 0.42),
        <= 60 => new FactorStatus("update-rate", "stable", "steady", 0.6, 0.42),
        <= 120 => new FactorStatus("update-rate", "stable", "cool", 0.5, 0.42),
        <= 240 => new FactorStatus("update-rate", "stale", "aging", 0.36, 0.42),
        _ => new FactorStatus("update-rate", "stale", "dormant", 0.2, 0.42)
    };

    private static FactorStatus BuildDownloadState(double ratio) => ratio switch
    {
        < 0.25 => new FactorStatus("downloads", "underdog", "emerging", 0.34, 0.35),
        < 0.5 => new FactorStatus("downloads", "underdog", "rising", 0.46, 0.35),
        < 0.85 => new FactorStatus("downloads", "mainstream", "steady", 0.58, 0.35),
        < 1.25 => new FactorStatus("downloads", "mainstream", "solid", 0.7, 0.35),
        < 2.0 => new FactorStatus("downloads", "popular", "trending", 0.83, 0.35),
        _ => new FactorStatus("downloads", "popular", "hot", 0.95, 0.35)
    };

    private static FactorStatus BuildReviewState(double avg, int count)
    {
        if (count == 0)
        {
            return new FactorStatus("reviews", "nascent", "unreviewed", 0.4, 0.23);
        }

        if (avg >= 4.6)
        {
            return new FactorStatus("reviews", "trusted", count >= 20 ? "beloved" : "praised", 0.94, 0.23);
        }

        if (avg >= 4.0)
        {
            return new FactorStatus("reviews", "trusted", count >= 8 ? "well-reviewed" : "promising", 0.82, 0.23);
        }

        if (avg >= 3.0)
        {
            return new FactorStatus("reviews", "mixed", "in-progress", 0.63, 0.23);
        }

        return new FactorStatus("reviews", "warning", "critical", 0.34, 0.23);
    }

    private async Task ToggleSortDirectionAsync()
    {
        SortDescending = !SortDescending;
        await LoadAsync();
    }

    private async Task ApplyFiltersAsync() => await LoadAsync();

    private async Task ResetAsync()
    {
        Search = string.Empty;
        SelectedTag = "all";
        SelectedTopic = "all";
        SelectedStatus = "all";
        SelectedSort = "popularity";
        MinReviews = 0;
        SortDescending = true;
        await LoadAsync();
    }

    private sealed record PackageBrowseRow(
        Guid Id,
        string Name,
        string Description,
        string Category,
        string? RepositoryUrl,
        string? WebsiteUrl,
        bool IsPublic,
        long TotalDownloads,
        DateTimeOffset UpdatedAtUtc,
        int PendingReviewsCount,
        double AverageRating,
        int ReviewCount,
        IReadOnlyList<string> Tags,
        PackageHealthStatus Status)
    {
        public string Topic { get; init; } = string.Empty;
    }

    private sealed record PackageHealthStatus(
        string State,
        string SubState,
        double Score,
        FactorStatus UpdateRate,
        FactorStatus Downloads,
        FactorStatus Reviews);

    private sealed record FactorStatus(
        string Factor,
        string State,
        string SubState,
        double Normalized,
        double Weight);

    private sealed class StatusScoreBuilder
    {
        private double _weighted;
        private double _weightTotal;

        private StatusScoreBuilder() { }

        public static StatusScoreBuilder Create() => new();

        public StatusScoreBuilder Add(double weight, double normalized)
        {
            var clampedWeight = Math.Max(0, weight);
            var clampedNormalized = Math.Clamp(normalized, 0, 1);
            _weighted += clampedWeight * clampedNormalized;
            _weightTotal += clampedWeight;
            return this;
        }

        public double Build() => _weightTotal <= 0 ? 0 : _weighted / _weightTotal;
    }
}
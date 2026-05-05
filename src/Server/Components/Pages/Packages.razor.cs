using Microsoft.AspNetCore.Components;
using System.Net.Http.Json;
using Server.Services;
using Server.Features.Packages;
using Microsoft.AspNetCore.WebUtilities;

namespace Server.Components.Pages;

public partial class Packages
{
    [Inject] private HttpClient Http { get; set; } = default!;

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
        var query = new Dictionary<string, string?>
        {
            ["q"] = Search,
            ["tag"] = SelectedTag,
            ["topic"] = SelectedTopic,
            ["status"] = SelectedStatus,
            ["sort"] = SelectedSort,
            ["order"] = SortDescending ? "desc" : "asc",
            ["minReviews"] = MinReviews.ToString(),
            ["limit"] = "100"
        };

        var url = QueryHelpers.AddQueryString("/api/search", query);
        var packageRows = await Http.GetFromJsonAsync<List<PackageSearchResponse>>(url) ?? [];

        AvailableTopics.Clear();
        AvailableTopics.AddRange(packageRows
            .Select(x => x.Package.Category)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x));

        var rows = packageRows
            .Select(x =>
            {
                var normalizedTags = x.Package.Tags
                    .Append(x.Package.Category)
                    .Where(t => !string.IsNullOrWhiteSpace(t))
                    .Select(t => t.Trim().ToLowerInvariant())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                var status = BuildStatus(x.Health);
                return new PackageBrowseRow(
                x.Package.Id,
                x.Package.Name,
                x.Package.Description,
                x.Package.Category,
                x.Package.RepositoryUrl,
                x.Package.WebsiteUrl,
                x.Package.IsPublic,
                x.Package.TotalDownloads,
                x.Package.UpdatedAtUtc,
                x.Package.PendingReviewsCount,
                x.Package.AverageRating,
                x.ReviewCount,
                normalizedTags,
                status,
                x.Package.OwnerUserId,
                x.Package.OwnerDisplayName,
                x.Package.OwnerIsPublisherVerified)
                {
                    Topic = x.Package.Category
                };
            })
            .ToList();

        AvailableTags.Clear();
        AvailableTags.AddRange(rows
            .SelectMany(x => x.Tags)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x));

        Rows.Clear();
        Rows.AddRange(rows.Take(100));
    }

    private static PackageHealthStatus BuildStatus(PackageHealthSnapshotResponse health)
        => new(
            health.State,
            health.SubState,
            health.Score,
            new FactorStatus("update-rate", health.UpdateRateState, health.UpdateRateSubState, health.UpdateRateNormalized, health.UpdateRateWeight),
            new FactorStatus("downloads", health.DownloadsState, health.DownloadsSubState, health.DownloadsNormalized, health.DownloadsWeight),
            new FactorStatus("reviews", health.ReviewsState, health.ReviewsSubState, health.ReviewsNormalized, health.ReviewsWeight));

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
        PackageHealthStatus Status,
        string OwnerUserId,
        string OwnerDisplayName,
        bool OwnerIsPublisherVerified)
    {
        public string Topic { get; init; } = string.Empty;
    }
}
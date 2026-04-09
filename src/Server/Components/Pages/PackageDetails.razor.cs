using System.Security.Claims;
using System.Net;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.FluentUI.AspNetCore.Components;
using Server.Components.Shared;
using Server.Data;
using Server.Features.Packages;
using Server.Services;

namespace Server.Components.Pages;

public partial class PackageDetails
{
    private const string PackageBoardEntityType = "Package";

    [Parameter] public string PackageName { get; set; } = string.Empty;
    private PackageEntity? Package;
    private readonly List<PackageCommunityReviewEntity> Reviews = [];
    private readonly List<PackageVersionSummaryResponse> Versions = [];
    private readonly List<PackageDependencyResponse> Dependencies = [];
    [Inject] private HttpClient Http { get; set; } = default!;
    [Inject] private IDialogService DialogService { get; set; } = default!;
    [Inject] private IHtmlSanitizationService HtmlSanitization { get; set; } = default!;
    private bool IsFollowing;
    private int PackageBoardId;
    private bool IsPackageBoardLocked;
    private string SelectedTabId = "pkg-tab-versions";
    private PackageHealthStatus? HealthStatus;
    private int DependentsCount;
    private string? LatestReadme;
    private PackageVersionSummaryResponse? LatestVersion;
    private bool IsAuthenticated => HttpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated ?? false;
    private bool CanManageVersions => IsAuthenticated && (HttpContextAccessor.HttpContext?.User?.IsInRole("SuperAdmin") == true || Package?.OwnerUserId == HttpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier));
    private double AverageReviewRating => Reviews.Count == 0 ? 0d : Reviews.Average(x => x.Rating);
    private int HealthStars => Math.Clamp((int)Math.Round((HealthStatus?.Score ?? 0d) * 5, MidpointRounding.AwayFromZero), 1, 5);
    private double HealthPercent => (HealthStatus?.Score ?? 0d) * 100d;
    private MarkupString RenderReviewHtml(string html) => new(HtmlSanitization.Sanitize(html));

    protected override async Task OnParametersSetAsync()
    {
        Package = await DbContext.Packages.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Name == PackageName && x.IsPublic);
        await EnsurePackageBoardAsync();
        await LoadSecondaryDataAsync();
        await LoadFollowAsync();
    }

    private async Task EnsurePackageBoardAsync()
    {
        PackageBoardId = 0;
        IsPackageBoardLocked = false;

        if (Package is null)
        {
            return;
        }

        var entityId = Package.Id.ToString();
        var board = await DbContext.Boards
            .SingleOrDefaultAsync(x => x.EntityType == PackageBoardEntityType && x.EntityId == entityId);

        if (board is null)
        {
            board = new BoardEntity
            {
                Name = $"{Package.Name} discussions",
                Slug = $"pkg-{Package.Id:N}",
                Description = $"Community discussions for {Package.Name}",
                EntityType = PackageBoardEntityType,
                EntityId = entityId,
                CreatedAtUtc = DateTime.UtcNow,
                IsLocked = false
            };

            DbContext.Boards.Add(board);
            await DbContext.SaveChangesAsync();
        }

        PackageBoardId = board.Id;
        IsPackageBoardLocked = board.IsLocked;
    }

    private async Task LoadSecondaryDataAsync()
    {
        Reviews.Clear();
        Versions.Clear();
        Dependencies.Clear();
        DependentsCount = 0;
        LatestReadme = null;
        LatestVersion = null;
        HealthStatus = null;

        if (Package is null)
        {
            return;
        }

        var reviewRows = await DbContext.PackageCommunityReviews
            .AsNoTracking()
            .Where(x => x.PackageId == Package.Id)
            .ToListAsync();

        Reviews.AddRange(reviewRows
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(30));

        var averageDownloads = await DbContext.Packages
            .AsNoTracking()
            .Where(x => x.IsPublic)
            .AverageAsync(x => (double?)x.TotalDownloads) ?? 0d;

        HealthStatus = PackageHealthScoring.Calculate(Package, averageDownloads, AverageReviewRating, Reviews.Count);

        var versionRows = await DbContext.PackageVersions
            .AsNoTracking()
            .Where(x => x.PackageId == Package.Id)
            .ToListAsync();

        var orderedVersionRows = versionRows
            .OrderByDescending(x => x.PublishedAtUtc)
            .ToList();

        Versions.AddRange(orderedVersionRows.Select(x => new PackageVersionSummaryResponse(
            x.Id,
            x.PackageId,
            Package.Name,
            x.Version,
            x.IsYanked,
            x.ChecksumSha256,
            x.SizeBytes,
            x.PublishedAtUtc,
            x.YankedAtUtc)));

        LatestVersion = Versions.FirstOrDefault();
        var manifest = PackageManifestMetadataReader.Read(orderedVersionRows.FirstOrDefault()?.ManifestJson);
        LatestReadme = manifest.Readme;
        Dependencies.AddRange(manifest.Dependencies.Select(d => new PackageDependencyResponse(
            d.Name,
            d.Version,
            d.Source,
            d.Registry)));

        var otherVersionRows = await DbContext.PackageVersions
            .AsNoTracking()
            .Where(x => x.PackageId != Package.Id)
            .Select(x => new { x.PackageId, x.PublishedAtUtc, x.ManifestJson })
            .ToListAsync();

        var otherLatestManifests = otherVersionRows
            .GroupBy(x => x.PackageId)
            .Select(group => group
                .OrderByDescending(x => x.PublishedAtUtc)
                .Select(x => x.ManifestJson)
                .FirstOrDefault())
            .ToList();

        DependentsCount = otherLatestManifests.Count(item =>
            PackageManifestMetadataReader.Read(item).Dependencies.Any(d =>
                string.Equals(d.Name, Package.Name, StringComparison.OrdinalIgnoreCase)));
    }

    private async Task OpenReviewDialogAsync()
    {
        if (!IsAuthenticated || Package is null)
        {
            return;
        }

        var content = new PackageReviewDialog.ReviewInput
        {
            PackageName = Package.Name,
            Rating = 5,
            Comment = string.Empty
        };

        var parameters = new DialogParameters
        {
            Width = "min(620px, calc(100vw - 32px))",
            Modal = true,
            TrapFocus = true,
            PreventDismissOnOverlayClick = true
        };

        var dialog = await DialogService.ShowDialogAsync<PackageReviewDialog>(content, parameters);
        var result = await dialog.Result;
        if (result?.Cancelled != false || result.Data is not PackageReviewDialog.ReviewInput review)
        {
            return;
        }

        await AddReviewAsync(review.Rating, review.Comment);
    }

    private async Task AddReviewAsync(int rating, string comment)
    {
        var userId = HttpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (Package is null || string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(comment))
        {
            return;
        }

        DbContext.PackageCommunityReviews.Add(new PackageCommunityReviewEntity
        {
            Id = Guid.NewGuid(),
            PackageId = Package.Id,
            UserId = userId,
            Rating = Math.Clamp(rating, 1, 5),
            Comment = HtmlSanitization.Sanitize(comment),
            CreatedAtUtc = DateTimeOffset.UtcNow,
        });
        await DbContext.SaveChangesAsync();

        await LoadSecondaryDataAsync();
    }

    private int GetReviewCountFor(int rating)
        => Reviews.Count(x => x.Rating == rating);

    private double GetReviewDistributionPercent(int rating)
        => Reviews.Count == 0 ? 0d : (GetReviewCountFor(rating) * 100d) / Reviews.Count;

    private async Task ToggleYankVersionAsync(PackageVersionSummaryResponse version)
    {
        if (!CanManageVersions)
        {
            return;
        }

        var packageName = Uri.EscapeDataString(PackageName);
        var versionValue = Uri.EscapeDataString(version.Version);
        var route = version.IsYanked
            ? $"/api/packages/{packageName}/versions/{versionValue}/unyank"
            : $"/api/packages/{packageName}/versions/{versionValue}/yank";

        try
        {
            var response = await Http.PostAsync(route, content: null);
            if (response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.Conflict)
            {
                await LoadSecondaryDataAsync();
            }
        }
        catch
        {
            // Keep the page stable if the operation fails.
        }
    }

    private async Task LoadFollowAsync()
    {
        if (!IsAuthenticated || Package is null) return;
        try
        {
            var resp = await Http.GetAsync($"/api/users/follows/packages/is-following?packageId={Uri.EscapeDataString(Package.Id.ToString())}");
            if (!resp.IsSuccessStatusCode) return;
            var payload = await resp.Content.ReadFromJsonAsync<IsFollowingResponse>();
            if (payload is not null) IsFollowing = payload.IsFollowing;
        }
        catch { }
    }

    private async Task ToggleFollowAsync()
    {
        if (!IsAuthenticated || Package is null) return;
        try
        {
            var payload = new TogglePackageFollowRequest { PackageId = Package.Id.ToString() };
            var resp = await Http.PostAsJsonAsync("/api/users/follows/packages/toggle", payload);
            if (!resp.IsSuccessStatusCode) return;
            var result = await resp.Content.ReadFromJsonAsync<TogglePackageFollowResponse>();
            if (result is not null) IsFollowing = result.IsFollowing;
        }
        catch { }
    }

    private sealed class IsFollowingResponse { public bool IsFollowing { get; set; } }
    private sealed class TogglePackageFollowRequest { public string PackageId { get; set; } = string.Empty; }
    private sealed class TogglePackageFollowResponse { public bool IsFollowing { get; set; } }

    private static string FormatSize(long bytes)
    {
        string[] suf = { "B", "KB", "MB", "GB", "TB" };
        if (bytes == 0) return "0 B";
        var bytesDouble = (double)bytes;
        var place = Convert.ToInt32(Math.Floor(Math.Log(bytesDouble, 1024)));
        var num = Math.Round(bytesDouble / Math.Pow(1024, place), 1);
        return $"{num} {suf[place]}";
    }
}
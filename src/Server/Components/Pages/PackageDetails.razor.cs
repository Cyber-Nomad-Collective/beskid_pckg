using System.Security.Claims;
using System.Net;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.FluentUI.AspNetCore.Components;
using Icons = Microsoft.FluentUI.AspNetCore.Components.Icons;
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
    private bool CanModerateBoardUser;
    private string SelectedTabId = "pkg-tab-versions";
    private PackageHealthStatus? HealthStatus;
    private int DependentsCount;
    private string? LatestReadme;
    private PackageVersionSummaryResponse? LatestVersion;
    private string ExplorerVersion = "latest";
    private DateTimeOffset? _firstPublishedAtUtc;
    private DateTimeOffset? _lastPublishedAtUtc;
    private string? _heroLatestVersion;
    private bool IsAuthenticated => HttpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated ?? false;
    private bool CanManageVersions => IsAuthenticated && (HttpContextAccessor.HttpContext?.User?.IsInRole("SuperAdmin") == true || Package?.OwnerUserId == HttpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier));
    private double AverageReviewRating => Reviews.Count == 0 ? 0d : Reviews.Average(x => x.Rating);
    private int HealthStars => Math.Clamp((int)Math.Round((HealthStatus?.Score ?? 0d) * 5, MidpointRounding.AwayFromZero), 1, 5);
    private double HealthPercent => (HealthStatus?.Score ?? 0d) * 100d;
    private MarkupString RenderReviewHtml(string html) => new(HtmlSanitization.Sanitize(html));
    private bool _packageIconFailed;
    private Guid? _packageIconContextId;
    private string _ownerPublisherUserId = string.Empty;
    private string _ownerPublisherDisplayName = string.Empty;
    private bool _ownerPublisherVerified;

    protected override async Task OnParametersSetAsync()
    {
        var uid = HttpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        var isSuperAdmin = HttpContextAccessor.HttpContext?.User?.IsInRole("SuperAdmin") == true;
        Package = await DbContext.Packages.AsNoTracking()
            .SingleOrDefaultAsync(x =>
                x.Name == PackageName
                && (x.IsPublic
                    || (!string.IsNullOrWhiteSpace(uid) && (isSuperAdmin || x.OwnerUserId == uid))));
        if (Package?.Id != _packageIconContextId)
        {
            _packageIconContextId = Package?.Id;
            _packageIconFailed = false;
        }
        await EnsurePackageBoardAsync();
        await RefreshBoardModerationAsync();
        await LoadSecondaryDataAsync();
        await LoadFollowAsync();
    }

    private void OnPackageIconError() => _packageIconFailed = true;

    private async Task RefreshBoardModerationAsync()
    {
        CanModerateBoardUser = false;
        if (PackageBoardId <= 0)
        {
            return;
        }

        var uid = HttpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(uid))
        {
            return;
        }

        CanModerateBoardUser = await Authorization.CanModerateBoardAsync(uid, PackageBoardId);
    }

    private async Task HandleBoardLockChangedAsync()
    {
        await EnsurePackageBoardAsync();
        await RefreshBoardModerationAsync();
        StateHasChanged();
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
        _firstPublishedAtUtc = null;
        _lastPublishedAtUtc = null;
        _heroLatestVersion = null;

        if (Package is null)
        {
            _ownerPublisherUserId = string.Empty;
            _ownerPublisherDisplayName = string.Empty;
            _ownerPublisherVerified = false;
            return;
        }

        var ownerRow = await DbContext.Users.AsNoTracking()
            .Where(u => u.Id == Package.OwnerUserId)
            .Select(u => new { u.DisplayName, u.UserName, u.IsPublisherVerified })
            .FirstOrDefaultAsync();

        _ownerPublisherUserId = Package.OwnerUserId;
        _ownerPublisherDisplayName = ownerRow is null
            ? Package.OwnerUserId
            : PackageOwnerQueries.PublisherDisplayLabel(ownerRow.DisplayName, ownerRow.UserName);
        if (string.IsNullOrWhiteSpace(_ownerPublisherDisplayName))
        {
            _ownerPublisherDisplayName = Package.OwnerUserId;
        }

        _ownerPublisherVerified = ownerRow?.IsPublisherVerified ?? false;

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

        if (versionRows.Count > 0)
        {
            _firstPublishedAtUtc = versionRows.Min(x => x.PublishedAtUtc);
            var activeRows = versionRows.Where(x => !x.IsYanked).ToList();
            _lastPublishedAtUtc = activeRows.Count > 0
                ? activeRows.Max(x => x.PublishedAtUtc)
                : versionRows.Max(x => x.PublishedAtUtc);
            _heroLatestVersion = PackageVersioning.GetLatestNonYankedVersionString(
                versionRows.Select(x => (x.Version, x.IsYanked)));
        }

        LatestVersion = Versions.FirstOrDefault();
        ExplorerVersion = LatestVersion?.Version ?? "latest";
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

        await AddReviewAsync(review.Rating, review.Comment, review.CaptchaToken);
    }

    private async Task AddReviewAsync(int rating, string comment, string? captchaToken)
    {
        if (Package is null || string.IsNullOrWhiteSpace(comment))
        {
            return;
        }

        var payload = new
        {
            rating = Math.Clamp(rating, 1, 5),
            comment,
            captchaToken,
        };

        try
        {
            var response = await Http.PostAsJsonAsync(
                $"/api/packages/{Uri.EscapeDataString(Package.Name)}/community-reviews",
                payload);
            if (response.IsSuccessStatusCode)
            {
                await LoadSecondaryDataAsync();
            }
        }
        catch
        {
            // Keep the page stable if the operation fails.
        }
    }

    private int GetReviewCountFor(int rating)
        => Reviews.Count(x => x.Rating == rating);

    private double GetReviewDistributionPercent(int rating)
        => Reviews.Count == 0 ? 0d : (GetReviewCountFor(rating) * 100d) / Reviews.Count;

    private IReadOnlyList<GridActionDefinition> GetVersionRowActions(PackageVersionSummaryResponse version)
    {
        if (!CanManageVersions)
        {
            return Array.Empty<GridActionDefinition>();
        }

        if (version.IsYanked)
        {
            return
            [
                new GridActionDefinition
                {
                    Icon = new Icons.Regular.Size20.ArrowCounterclockwise(),
                    Tooltip = "Unyank version",
                    Appearance = Appearance.Accent,
                    OnClick = EventCallback.Factory.Create(this, () => ToggleYankVersionAsync(version))
                }
            ];
        }

        return
        [
            new GridActionDefinition
            {
                Icon = new Icons.Regular.Size20.Prohibited(),
                Tooltip = "Yank version",
                OnClick = EventCallback.Factory.Create(this, () => ToggleYankVersionAsync(version))
            }
        ];
    }

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

    private void OpenDocsFullPage()
    {
        if (Package is null)
        {
            return;
        }

        var ver = string.IsNullOrWhiteSpace(ExplorerVersion) ? "latest" : ExplorerVersion;
        Navigation.NavigateTo(AppDocumentationRoutes.AppDocsBase(Package.Name, ver));
    }

    private string EmbedOrigin => Navigation.BaseUri.TrimEnd('/');

    private string EmbedBadgeAbsoluteUrl =>
        Package is { IsPublic: true }
            ? EmbedOrigin + PackageEmbedUrls.BadgeRelativePath(Package.Name)
            : string.Empty;

    private string EmbedCardAbsoluteUrl =>
        Package is { IsPublic: true }
            ? EmbedOrigin + PackageEmbedUrls.CardRelativePath(Package.Name)
            : string.Empty;

    private string EmbedPackagePageUrl =>
        Package is null ? string.Empty : $"{EmbedOrigin}/packages/{Uri.EscapeDataString(Package.Name)}";

    private string EmbedBadgeMarkdownLinked =>
        Package is { IsPublic: true }
            ? $"[![{EscapeMarkdownAltText(Package.Name)} on Beskid registry]({EmbedBadgeAbsoluteUrl})]({EmbedPackagePageUrl})"
            : string.Empty;

    private string EmbedBadgeMarkdownImageOnly =>
        Package is { IsPublic: true }
            ? $"![Beskid registry]({EmbedBadgeAbsoluteUrl})"
            : string.Empty;

    private string EmbedWidgetHtml =>
        Package is { IsPublic: true }
            ? $"""<iframe src="{EmbedCardAbsoluteUrl}" title="{WebUtility.HtmlEncode(Package.Name + " on Beskid registry")}" width="480" height="200" style="border:0;border-radius:8px;max-width:100%;" loading="lazy" referrerpolicy="no-referrer-when-downgrade"></iframe>"""
            : string.Empty;

    private static string EscapeMarkdownAltText(string name)
        => name.Replace("[", string.Empty, StringComparison.Ordinal)
            .Replace("]", string.Empty, StringComparison.Ordinal)
            .Replace("\r", string.Empty, StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal);

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
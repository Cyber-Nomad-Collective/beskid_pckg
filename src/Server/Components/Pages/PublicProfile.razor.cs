using Microsoft.AspNetCore.Components;
using Microsoft.FluentUI.AspNetCore.Components;
using Server.Components.Shared;
using Server.Features.Users;

namespace Server.Components.Pages;

public partial class PublicProfile
{
    [Inject] private HttpClient Http { get; set; } = default!;

    [Parameter] public string UserId { get; set; } = string.Empty;

    private bool IsLoading = true;
    private string? LoadedUserId;
    private string? FeedbackMessage;
    private PublicProfilePayload? Profile;
    private IReadOnlyList<SocialLinkViewModel> SocialLinks => BuildSocialLinks(Profile);
    [Inject] private IHttpContextAccessor HttpContextAccessor { get; set; } = default!;
    private bool IsAuthenticated => HttpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated ?? false;
    private bool IsFollowingPublisher;

    private static IReadOnlyList<SocialLinkViewModel> BuildSocialLinks(PublicProfilePayload? profile)
    {
        if (profile is null)
        {
            return [];
        }

        if (profile.SocialLinks.Count > 0)
        {
            return profile.SocialLinks
                .Where(x => !string.IsNullOrWhiteSpace(x.Url))
                .Select(x => new SocialLinkViewModel(x.Url.Trim(), x.Platform))
                .ToList();
        }

        var legacyUrls = new[] { profile.GitHubUrl, profile.WebsiteUrl, profile.XUrl };
        return legacyUrls
            .Where(url => !string.IsNullOrWhiteSpace(url))
            .Select(url => url!.Trim())
            .Select(url => new SocialLinkViewModel(url, SocialPlatformCatalog.DetectPlatform(url)))
            .ToList();
    }

    protected override async Task OnParametersSetAsync()
    {
        if (string.IsNullOrWhiteSpace(UserId))
        {
            IsLoading = false;
            FeedbackMessage = "Profile not found.";
            Profile = null;
            return;
        }

        if (string.Equals(LoadedUserId, UserId, StringComparison.Ordinal))
        {
            return;
        }

        LoadedUserId = UserId;
        await LoadProfileAsync();
        await LoadFollowAsync();
    }

    private async Task LoadProfileAsync()
    {
        IsLoading = true;
        FeedbackMessage = null;
        Profile = null;

        try
        {
            var response = await Http.GetAsync($"/api/users/public/{Uri.EscapeDataString(UserId)}");
            var payload = await TryReadJsonAsync<PublicProfileResponse>(response);

            if (!response.IsSuccessStatusCode || payload is null || !payload.Success || payload.Profile is null)
            {
                var fallback = await ReadErrorBodyAsync(response);
                FeedbackMessage = payload?.Message ?? fallback ?? "Unable to load profile.";
                return;
            }

            Profile = payload.Profile;
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task LoadFollowAsync()
    {
        if (!IsAuthenticated || string.IsNullOrWhiteSpace(UserId)) return;
        try
        {
            var resp = await Http.GetAsync($"/api/users/follows/publishers/is-following?publisherUserId={Uri.EscapeDataString(UserId)}");
            if (!resp.IsSuccessStatusCode) return;
            var payload = await resp.Content.ReadFromJsonAsync<IsFollowingResponse>();
            if (payload is not null) IsFollowingPublisher = payload.IsFollowing;
        }
        catch { }
    }

    private async Task ToggleFollowPublisherAsync()
    {
        if (!IsAuthenticated || string.IsNullOrWhiteSpace(UserId)) return;
        try
        {
            var payload = new TogglePublisherFollowRequest { PublisherUserId = UserId };
            var resp = await Http.PostAsJsonAsync("/api/users/follows/publishers/toggle", payload);
            if (!resp.IsSuccessStatusCode) return;
            var result = await resp.Content.ReadFromJsonAsync<TogglePublisherFollowResponse>();
            if (result is not null) IsFollowingPublisher = result.IsFollowing;
        }
        catch { }
    }

    private sealed class IsFollowingResponse { public bool IsFollowing { get; set; } }
    private sealed class TogglePublisherFollowRequest { public string PublisherUserId { get; set; } = string.Empty; }
    private sealed class TogglePublisherFollowResponse { public bool IsFollowing { get; set; } }

    private static bool IsJsonResponse(HttpResponseMessage response)
    {
        var mediaType = response.Content.Headers.ContentType?.MediaType;
        return !string.IsNullOrWhiteSpace(mediaType)
               && (mediaType.Contains("json", StringComparison.OrdinalIgnoreCase)
                   || mediaType.EndsWith("+json", StringComparison.OrdinalIgnoreCase));
    }

    private static async Task<T?> TryReadJsonAsync<T>(HttpResponseMessage response)
    {
        if (!IsJsonResponse(response))
        {
            return default;
        }

        try
        {
            return await response.Content.ReadFromJsonAsync<T>();
        }
        catch
        {
            return default;
        }
    }

    private static async Task<string?> ReadErrorBodyAsync(HttpResponseMessage response)
    {
        try
        {
            var body = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(body))
            {
                return null;
            }

            return body.Length > 240 ? body[..240] : body;
        }
        catch
        {
            return null;
        }
    }

    private static Icon GetActivityIcon(string activityType)
    {
        return activityType.ToLowerInvariant() switch
        {
            "version" => new Microsoft.FluentUI.AspNetCore.Components.Icons.Regular.Size16.Box(),
            "review" => new Microsoft.FluentUI.AspNetCore.Components.Icons.Regular.Size16.Star(),
            "issue" => new Microsoft.FluentUI.AspNetCore.Components.Icons.Regular.Size16.Alert(),
            "board-post" => new Microsoft.FluentUI.AspNetCore.Components.Icons.Regular.Size16.Chat(),
            "board-comment" => new Microsoft.FluentUI.AspNetCore.Components.Icons.Regular.Size16.Comment(),
            "package_published" => new Microsoft.FluentUI.AspNetCore.Components.Icons.Regular.Size16.Box(),
            "package_updated" => new Microsoft.FluentUI.AspNetCore.Components.Icons.Regular.Size16.ArrowSync(),
            "package_downloaded" => new Microsoft.FluentUI.AspNetCore.Components.Icons.Regular.Size16.ArrowDownload(),
            _ => new Microsoft.FluentUI.AspNetCore.Components.Icons.Regular.Size16.Circle()
        };
    }

    private sealed record SocialLinkViewModel(string Url, SocialPlatform Platform);
}

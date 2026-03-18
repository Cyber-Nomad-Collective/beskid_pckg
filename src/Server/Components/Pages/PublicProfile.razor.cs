using Microsoft.AspNetCore.Components;
using System.Net.Http.Json;
using pckg.Features.Users;

namespace Server.Components.Pages;

public partial class PublicProfile
{
    [Inject] private HttpClient Http { get; set; } = default!;

    [Parameter] public string UserId { get; set; } = string.Empty;

    private bool IsLoading = true;
    private string? LoadedUserId;
    private string? FeedbackMessage;
    private PublicProfilePayload? Profile;

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
}

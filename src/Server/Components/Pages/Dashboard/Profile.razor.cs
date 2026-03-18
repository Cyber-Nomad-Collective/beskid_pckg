using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using System.Net.Http.Json;

namespace Server.Components.Pages.Dashboard;

public partial class Profile
{
    private readonly ProfileFormModel Form = new();
    private bool IsLoading = true;
    private bool IsSaving;
    private string? FeedbackMessage;
    private bool FeedbackIsError;
    private string? UserId;
    private string? ProfileImageUrl;
    [Inject]
    public IHttpContextAccessor HttpContextAccessor { get; set; } = default!;
    [Inject]
    public HttpClient ApiHttp { get; set; } = default!;
    
    private bool IsSuperAdmin => HttpContextAccessor.HttpContext?.User?.IsInRole("SuperAdmin") == true;

    protected override async Task OnInitializedAsync()
    {
        await LoadProfileAsync();
    }

    private async Task LoadProfileAsync()
    {
        IsLoading = true;
        try
        {
            var response = await ApiHttp.GetAsync("/api/users/me");
            if (!response.IsSuccessStatusCode)
            {
                return;
            }

            var profile = await response.Content.ReadFromJsonAsync<CurrentUserResponse>();
            if (profile is null || !profile.IsAuthenticated)
            {
                return;
            }

            UserId = profile.UserId;
            Form.Email = profile.Email ?? string.Empty;
            Form.DisplayName = profile.DisplayName ?? string.Empty;
            Form.Bio = profile.Bio ?? string.Empty;
            Form.GitHubUrl = profile.GitHubUrl ?? string.Empty;
            Form.WebsiteUrl = profile.WebsiteUrl ?? string.Empty;
            Form.XUrl = profile.XUrl ?? string.Empty;
            ProfileImageUrl = profile.ProfileImageUrl;
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task SaveProfileAsync()
    {
        IsSaving = true;
        try
        {
            var response = await ApiHttp.PutAsJsonAsync(
                "/api/users/profile",
                new UpdateProfileRequest(Form.DisplayName, Form.Bio, Form.GitHubUrl, Form.WebsiteUrl, Form.XUrl));

            var payload = await TryReadJsonAsync<UpdateProfileResponse>(response);
            if (!response.IsSuccessStatusCode || payload is null || !payload.Success)
            {
                var fallback = await ReadErrorBodyAsync(response);
                SetFeedback(payload?.Message ?? fallback ?? "Unable to save profile.", true);
                return;
            }

            ProfileImageUrl = payload.Profile?.ProfileImageUrl;
            SetFeedback(payload.Message, false);
        }
        finally
        {
            IsSaving = false;
        }
    }

    private async Task UploadProfileImageAsync(InputFileChangeEventArgs args)
    {
        if (args.FileCount == 0)
        {
            return;
        }

        var file = args.File;
        using var content = new MultipartFormDataContent();
        await using var stream = file.OpenReadStream(5 * 1024 * 1024);
        var imageContent = new StreamContent(stream);
        imageContent.Headers.ContentType = System.Net.Http.Headers.MediaTypeHeaderValue.Parse(file.ContentType);
        content.Add(imageContent, "image", file.Name);

        var response = await ApiHttp.PostAsync("/api/users/profile/image", content);
        var payload = await TryReadJsonAsync<UploadProfileImageResponse>(response);
        if (!response.IsSuccessStatusCode || payload is null || !payload.Success)
        {
            var fallback = await ReadErrorBodyAsync(response);
            SetFeedback(payload?.Message ?? fallback ?? "Unable to upload profile picture.", true);
            return;
        }

        ProfileImageUrl = payload.ProfileImageUrl;
        SetFeedback(payload.Message, false);
    }

    private void SetFeedback(string message, bool isError)
    {
        FeedbackMessage = message;
        FeedbackIsError = isError;
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

    private sealed class ProfileFormModel
    {
        public string Email { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Bio { get; set; } = string.Empty;
        public string GitHubUrl { get; set; } = string.Empty;
        public string WebsiteUrl { get; set; } = string.Empty;
        public string XUrl { get; set; } = string.Empty;
    }

    private sealed record CurrentUserResponse(
        bool IsAuthenticated,
        string? UserId,
        string? Email,
        bool IsPublisher,
        string? DisplayName,
        string? Bio,
        string? GitHubUrl,
        string? WebsiteUrl,
        string? XUrl,
        string? ProfileImageUrl);

    private sealed record UpdateProfileRequest(
        string? DisplayName,
        string? Bio,
        string? GitHubUrl,
        string? WebsiteUrl,
        string? XUrl);

    private sealed record UpdateProfileResponse(bool Success, string Message, ProfilePayload? Profile);

    private sealed record ProfilePayload(
        string? Email,
        string? DisplayName,
        string? Bio,
        string? GitHubUrl,
        string? WebsiteUrl,
        string? XUrl,
        string? ProfileImageUrl);

    private sealed record UploadProfileImageResponse(bool Success, string Message, string? ProfileImageUrl);
}
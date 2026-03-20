using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.FluentUI.AspNetCore.Components;
using Server.Components.Shared;
using Server.Features.Users;
using System.Net.Http.Json;

namespace Server.Components.Pages.Dashboard;

public partial class Profile
{
    private const long ProfileImageMaxUploadBytes = 10 * 1024 * 1024;
    private readonly ProfileFormModel Form = new();
    private List<SocialLinkItem> SocialLinks = [];
    private readonly List<UserEmailItem> UserEmails = [];
    private bool IsLoading = true;
    private bool IsSaving;
    private bool IsUploadingImage;
    private string? FeedbackMessage;
    private bool FeedbackIsError;
    private string? UserId;
    private string? ProfileImageUrl;
    private long ProfileImageCacheToken = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    private string? ProfileImageSrc => BuildCacheBustedImageUrl(ProfileImageUrl, ProfileImageCacheToken);
    private string NewEmailAddress = string.Empty;
    [Inject]
    public IHttpContextAccessor HttpContextAccessor { get; set; } = default!;
    [Inject]
    public HttpClient ApiHttp { get; set; } = default!;
    [Inject]
    public IDialogService DialogService { get; set; } = default!;

    private bool IsSuperAdmin => HttpContextAccessor.HttpContext?.User?.IsInRole("SuperAdmin") == true;

    protected override async Task OnInitializedAsync()
    {
        await LoadProfileAsync();
        await LoadEmailsAsync();
    }

    private static string? BuildCacheBustedImageUrl(string? imageUrl, long cacheToken)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
        {
            return imageUrl;
        }

        var separator = imageUrl.Contains('?', StringComparison.Ordinal) ? '&' : '?';
        return $"{imageUrl}{separator}v={cacheToken}";
    }

    private void RefreshProfileImage()
    {
        ProfileImageCacheToken = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
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
            SocialLinks = profile.SocialLinks
                .Select(link => new SocialLinkItem { Platform = link.Platform, Url = link.Url })
                .ToList();
            ProfileImageUrl = profile.ProfileImageUrl;
            RefreshProfileImage();
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task<ProfileImageCropDialog.CropOutput?> OpenProfileImageCropDialogAsync(IBrowserFile file)
    {
        if (file.Size <= 0)
        {
            SetFeedback("Selected image is empty.", true);
            return null;
        }

        if (file.Size > ProfileImageMaxUploadBytes)
        {
            SetFeedback("Selected image exceeds the 10 MB limit.", true);
            return null;
        }

        var content = new ProfileImageCropDialog.CropInput(file, ProfileImageMaxUploadBytes);
        var parameters = new DialogParameters
        {
            Width = "min(720px, calc(100vw - 32px))",
            Modal = true,
            TrapFocus = true,
            PreventDismissOnOverlayClick = true
        };

        var dialog = await DialogService.ShowDialogAsync<ProfileImageCropDialog>(content, parameters);
        var result = await dialog.Result;
        if (result?.Cancelled != false || result.Data is not ProfileImageCropDialog.CropOutput cropped)
        {
            return null;
        }

        return cropped;
    }

    private async Task SaveProfileAsync()
    {
        IsSaving = true;
        try
        {
            var response = await ApiHttp.PutAsJsonAsync(
                "/api/users/profile",
                new UpdateProfileRequest(
                    Form.DisplayName,
                    Form.Bio,
                    SocialLinks
                        .Where(x => !string.IsNullOrWhiteSpace(x.Url))
                        .Select(x => new ProfileSocialLinkDto(x.Platform, x.Url.Trim()))
                        .ToList()));

            var payload = await TryReadJsonAsync<UpdateProfileResponse>(response);
            if (!response.IsSuccessStatusCode || payload is null || !payload.Success)
            {
                var fallback = await ReadErrorBodyAsync(response);
                SetFeedback(payload?.Message ?? fallback ?? "Unable to save profile.", true);
                return;
            }

            ProfileImageUrl = payload.Profile?.ProfileImageUrl;
            SetFeedback(payload.Message, false);
            RefreshProfileImage();
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
        var cropped = await OpenProfileImageCropDialogAsync(file);
        if (cropped is null)
        {
            return;
        }

        IsUploadingImage = true;
        try
        {
            using var content = new MultipartFormDataContent();
            var imageContent = new ByteArrayContent(cropped.Bytes);
            imageContent.Headers.ContentType = System.Net.Http.Headers.MediaTypeHeaderValue.Parse(cropped.ContentType);
            content.Add(imageContent, "image", cropped.FileName);

            var response = await ApiHttp.PostAsync("/api/users/profile/image", content);
            var payload = await TryReadJsonAsync<UploadProfileImageResponse>(response);
            if (!response.IsSuccessStatusCode || payload is null || !payload.Success)
            {
                var fallback = await ReadErrorBodyAsync(response);
                SetFeedback(payload?.Message ?? fallback ?? "Unable to upload profile picture.", true);
                return;
            }

            ProfileImageUrl = payload.ProfileImageUrl;
            RefreshProfileImage();
            SetFeedback("Profile photo uploaded successfully.", false);
        }
        catch (IOException)
        {
            SetFeedback("Selected image exceeds the 10 MB limit.", true);
        }
        finally
        {
            IsUploadingImage = false;
        }
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
            var mediaType = response.Content.Headers.ContentType?.MediaType;
            var body = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(body))
            {
                return null;
            }

            if (!string.IsNullOrWhiteSpace(mediaType)
                && mediaType.Contains("html", StringComparison.OrdinalIgnoreCase))
            {
                return $"Request failed ({(int)response.StatusCode}). Please try again.";
            }

            if (body.Contains("<!DOCTYPE", StringComparison.OrdinalIgnoreCase)
                || body.Contains("<html", StringComparison.OrdinalIgnoreCase))
            {
                return $"Request failed ({(int)response.StatusCode}). Please try again.";
            }

            return body.Length > 240 ? body[..240] : body;
        }
        catch
        {
            return null;
        }
    }

    private async Task LoadEmailsAsync()
    {
        try
        {
            var response = await ApiHttp.GetAsync("/api/users/emails");
            if (!response.IsSuccessStatusCode)
            {
                return;
            }

            var result = await response.Content.ReadFromJsonAsync<ListUserEmailsResponse>();
            if (result?.Emails is not null)
            {
                UserEmails.Clear();
                foreach (var email in result.Emails)
                {
                    UserEmails.Add(new UserEmailItem
                    {
                        Id = email.Id,
                        Email = email.Email,
                        IsVerified = email.IsVerified,
                        IsPrimary = email.IsPrimary
                    });
                }
            }
        }
        catch
        {
        }
    }

    private async Task AddEmailAsync()
    {
        if (string.IsNullOrWhiteSpace(NewEmailAddress))
        {
            return;
        }

        IsSaving = true;
        try
        {
            var response = await ApiHttp.PostAsJsonAsync(
                "/api/users/emails",
                new AddUserEmailRequest(NewEmailAddress));

            var payload = await TryReadJsonAsync<AddUserEmailResponse>(response);
            if (!response.IsSuccessStatusCode || payload is null || !payload.Success)
            {
                var fallback = await ReadErrorBodyAsync(response);
                SetFeedback(payload?.Message ?? fallback ?? "Unable to add email.", true);
                return;
            }

            SetFeedback("Email added successfully.", false);
            NewEmailAddress = string.Empty;
            await LoadEmailsAsync();
        }
        finally
        {
            IsSaving = false;
        }
    }

    private async Task RemoveEmailAsync(int emailId)
    {
        IsSaving = true;
        try
        {
            var response = await ApiHttp.DeleteAsync($"/api/users/emails/{emailId}");
            var payload = await TryReadJsonAsync<RemoveUserEmailResponse>(response);
            
            if (!response.IsSuccessStatusCode || payload is null || !payload.Success)
            {
                var fallback = await ReadErrorBodyAsync(response);
                SetFeedback(payload?.Message ?? fallback ?? "Unable to remove email.", true);
                return;
            }

            SetFeedback("Email removed successfully.", false);
            await LoadEmailsAsync();
        }
        finally
        {
            IsSaving = false;
        }
    }

    private sealed class ProfileFormModel
    {
        public string Email { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Bio { get; set; } = string.Empty;
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
        IReadOnlyList<ProfileSocialLinkDto> SocialLinks,
        string? ProfileImageUrl);

    private sealed record UpdateProfileRequest(
        string? DisplayName,
        string? Bio,
        IReadOnlyList<ProfileSocialLinkDto> SocialLinks);

    private sealed record UpdateProfileResponse(bool Success, string Message, ProfilePayload? Profile);

    private sealed record ProfilePayload(
        string? Email,
        string? DisplayName,
        string? Bio,
        string? GitHubUrl,
        string? WebsiteUrl,
        string? XUrl,
        IReadOnlyList<ProfileSocialLinkDto> SocialLinks,
        string? ProfileImageUrl);

    private sealed record ProfileSocialLinkDto(SocialPlatform Platform, string Url);

    private sealed record UploadProfileImageResponse(bool Success, string Message, string? ProfileImageUrl);

    private sealed class UserEmailItem
    {
        public int Id { get; set; }
        public string Email { get; set; } = string.Empty;
        public bool IsVerified { get; set; }
        public bool IsPrimary { get; set; }
    }

    private sealed record ListUserEmailsResponse(List<UserEmailDto> Emails);
    
    private sealed record UserEmailDto(int Id, string Email, bool IsVerified, bool IsPrimary, DateTime AddedAtUtc);
    
    private sealed record AddUserEmailRequest(string Email);
    
    private sealed record AddUserEmailResponse(bool Success, string Message);
    
    private sealed record RemoveUserEmailResponse(bool Success, string Message);
}
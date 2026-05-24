using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Components;
using Microsoft.FluentUI.AspNetCore.Components;

namespace Server.Components.Shared;

public partial class PackageReviewDialog : IDialogContentComponent<PackageReviewDialog.ReviewInput>
{
    [Parameter] public ReviewInput Content { get; set; } = new();
    [CascadingParameter] public FluentDialog Dialog { get; set; } = default!;
    [Inject] public HttpClient Http { get; set; } = default!;

    private bool _captchaEnabled;
    private RecaptchaEnterpriseV3? _reviewRecaptcha;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            var cfg = await Http.GetFromJsonAsync<CaptchaPublicConfigDto>("/api/public/captcha-config");
            _captchaEnabled = cfg?.CaptchaEnabled == true
                && !string.IsNullOrWhiteSpace(cfg.RecaptchaSiteKey);
        }
        catch
        {
            _captchaEnabled = false;
        }
    }

    private Task CancelAsync() => Dialog.CancelAsync();

    private async Task SubmitAsync()
    {
        Content.Comment = Content.Comment.Trim();
        if (string.IsNullOrWhiteSpace(Content.Comment))
        {
            return;
        }

        Content.CaptchaToken = null;
        if (_captchaEnabled)
        {
            if (_reviewRecaptcha is null)
            {
                return;
            }

            var token = await _reviewRecaptcha.ExecuteAsync();
            if (string.IsNullOrWhiteSpace(token))
            {
                return;
            }

            Content.CaptchaToken = token;
        }

        Content.Rating = Math.Clamp(Content.Rating, 1, 5);
        await Dialog.CloseAsync(Content);
    }

    private Task OnRatingChanged(int value)
    {
        Content.Rating = Math.Clamp(value, 1, 5);
        return Task.CompletedTask;
    }

    public sealed record ReviewInput
    {
        public string PackageName { get; init; } = string.Empty;
        public int Rating { get; set; } = 5;
        public string Comment { get; set; } = string.Empty;
        public string? CaptchaToken { get; set; }
    }

    private sealed record CaptchaPublicConfigDto(
        [property: JsonPropertyName("captchaEnabled")] bool CaptchaEnabled,
        [property: JsonPropertyName("recaptchaSiteKey")] string? RecaptchaSiteKey);
}

using System.Net.Http.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.FluentUI.AspNetCore.Components;

namespace Server.Components.Shared;

public partial class PackageReviewDialog : IDialogContentComponent<PackageReviewDialog.ReviewInput>
{
    [Parameter] public ReviewInput Content { get; set; } = new();
    [CascadingParameter] public FluentDialog Dialog { get; set; } = default!;
    [Inject] public HttpClient Http { get; set; } = default!;

    private string? _turnstileSiteKey;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            var cfg = await Http.GetFromJsonAsync<CaptchaPublicConfigDto>("/api/public/captcha-config");
            _turnstileSiteKey = string.IsNullOrWhiteSpace(cfg?.TurnstileSiteKey) ? null : cfg!.TurnstileSiteKey;
        }
        catch
        {
            _turnstileSiteKey = null;
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

        if (!string.IsNullOrWhiteSpace(_turnstileSiteKey) && string.IsNullOrWhiteSpace(Content.CaptchaToken))
        {
            return;
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

    private sealed record CaptchaPublicConfigDto(string TurnstileSiteKey);
}

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.FluentUI.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Server.Components.Shared;

public partial class ProfileImageCropDialog : IDialogContentComponent<ProfileImageCropDialog.CropInput>
{
    private const int OutputSizePx = 512;

    [Parameter] public CropInput Content { get; set; } = new();
    [CascadingParameter] public FluentDialog Dialog { get; set; } = default!;
    [Inject] public IJSRuntime JsRuntime { get; set; } = default!;

    private string? SourceDataUrl { get; set; }
    private bool IsBusy { get; set; }
    private string? ErrorMessage { get; set; }
    private decimal Zoom { get; set; } = 1m;
    private decimal OffsetX { get; set; }
    private decimal OffsetY { get; set; }

    protected override async Task OnInitializedAsync()
    {
        if (Content.File is null)
        {
            ErrorMessage = "No image was selected.";
            return;
        }

        try
        {
            await using var stream = Content.File.OpenReadStream(Content.MaxFileSizeBytes);
            using var memory = new MemoryStream();
            await stream.CopyToAsync(memory);

            var bytes = memory.ToArray();
            var contentType = string.IsNullOrWhiteSpace(Content.File.ContentType)
                ? "image/png"
                : Content.File.ContentType;

            SourceDataUrl = $"data:{contentType};base64,{Convert.ToBase64String(bytes)}";
        }
        catch (IOException)
        {
            ErrorMessage = "Selected image exceeds the 10 MB limit.";
        }
    }

    private Task OnZoomChanged(decimal value)
    {
        Zoom = value;
        return Task.CompletedTask;
    }

    private Task OnOffsetXChanged(decimal value)
    {
        OffsetX = value;
        return Task.CompletedTask;
    }

    private Task OnOffsetYChanged(decimal value)
    {
        OffsetY = value;
        return Task.CompletedTask;
    }

    private Task CancelAsync() => Dialog.CancelAsync();

    private async Task SubmitAsync()
    {
        if (string.IsNullOrWhiteSpace(SourceDataUrl))
        {
            ErrorMessage = "Image preview is not ready yet.";
            return;
        }

        IsBusy = true;
        ErrorMessage = null;

        try
        {
            var croppedDataUrl = await JsRuntime.InvokeAsync<string>(
                "profileImageCropper.cropSquareToPngDataUrl",
                SourceDataUrl,
                (double)Zoom,
                (double)OffsetX,
                (double)OffsetY,
                OutputSizePx);

            if (!TryExtractDataUrlPayload(croppedDataUrl, out var payload))
            {
                ErrorMessage = "Unable to crop image.";
                return;
            }

            var bytes = Convert.FromBase64String(payload);
            var outputName = BuildOutputFileName(Content.File?.Name ?? "profile.png");
            await Dialog.CloseAsync(new CropOutput(bytes, outputName, "image/png"));
        }
        catch (JSException)
        {
            ErrorMessage = "Cropping failed. Please try a different image.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private string BuildPreviewStyle()
    {
        if (string.IsNullOrWhiteSpace(SourceDataUrl))
        {
            return string.Empty;
        }

        var clampedZoom = Math.Clamp(Zoom, 1m, 3m);
        var clampedOffsetX = Math.Clamp(OffsetX, -1m, 1m);
        var clampedOffsetY = Math.Clamp(OffsetY, -1m, 1m);

        var zoomPercent = (double)(clampedZoom * 100m);
        var xPercent = 50d + (double)(clampedOffsetX * 25m);
        var yPercent = 50d + (double)(clampedOffsetY * 25m);

        return $"background-image:url('{SourceDataUrl}');background-size:{zoomPercent:F0}% auto;background-position:{xPercent:F1}% {yPercent:F1}%;";
    }

    private static bool TryExtractDataUrlPayload(string? dataUrl, out string payload)
    {
        payload = string.Empty;
        if (string.IsNullOrWhiteSpace(dataUrl))
        {
            return false;
        }

        var marker = ",";
        var markerIndex = dataUrl.IndexOf(marker, StringComparison.Ordinal);
        if (markerIndex < 0 || markerIndex + marker.Length >= dataUrl.Length)
        {
            return false;
        }

        payload = dataUrl[(markerIndex + marker.Length)..];
        return !string.IsNullOrWhiteSpace(payload);
    }

    private static string BuildOutputFileName(string sourceName)
    {
        var baseName = Path.GetFileNameWithoutExtension(sourceName);
        if (string.IsNullOrWhiteSpace(baseName))
        {
            baseName = "profile";
        }

        return $"{baseName}-cropped.png";
    }

    public sealed record CropInput(IBrowserFile? File = null, long MaxFileSizeBytes = 10 * 1024 * 1024);

    public sealed record CropOutput(byte[] Bytes, string FileName, string ContentType);
}

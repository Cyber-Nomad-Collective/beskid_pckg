using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace Server.Components.Shared;

public partial class UploadDropzone
{
    public enum UploadDropzoneMode
    {
        Fluent,
        Invisible
    }

    [Parameter] public string? Class { get; set; }
    [Parameter] public string? TriggerClass { get; set; }
    [Parameter] public string? Label { get; set; }
    [Parameter] public string PrimaryText { get; set; } = "File upload";
    [Parameter] public string? SecondaryText { get; set; }
    [Parameter] public string? HintText { get; set; }
    [Parameter] public string? Accept { get; set; }
    [Parameter] public string AriaLabel { get; set; } = "Choose file";
    [Parameter] public bool Disabled { get; set; }
    [Parameter] public bool AllowMultiple { get; set; }
    [Parameter] public bool HideMeta { get; set; }
    [Parameter] public bool ShowSelectedFileName { get; set; } = true;
    [Parameter] public UploadDropzoneMode Mode { get; set; } = UploadDropzoneMode.Fluent;
    [Parameter] public RenderFragment? TriggerContent { get; set; }
    [Parameter] public EventCallback<InputFileChangeEventArgs> OnFilesSelected { get; set; }

    private string? SelectedFileName { get; set; }

    private string RootClass
        => $"upload-dropzone upload-dropzone-mode-{Mode.ToString().ToLowerInvariant()} {(Disabled ? "is-disabled" : string.Empty)} {Class}".Trim();

    private string TriggerClassValue
        => $"upload-dropzone-trigger {(Mode == UploadDropzoneMode.Invisible ? "is-invisible-trigger" : string.Empty)} {TriggerClass}".Trim();

    private async Task HandleFilesSelectedAsync(InputFileChangeEventArgs args)
    {
        SelectedFileName = args.FileCount > 0 ? args.File.Name : null;

        if (OnFilesSelected.HasDelegate)
        {
            await OnFilesSelected.InvokeAsync(args);
        }
    }
}

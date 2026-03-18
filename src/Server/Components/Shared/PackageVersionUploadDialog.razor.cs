using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.FluentUI.AspNetCore.Components;

namespace Server.Components.Shared;

public partial class PackageVersionUploadDialog : IDialogContentComponent
{
    private bool IsHidden = true;
    private string PackageNameInput { get; set; } = string.Empty;
    private bool IsPackageLocked { get; set; }
    private string Version { get; set; } = string.Empty;
    private string ChecksumSha256 { get; set; } = string.Empty;
    private IBrowserFile? Artifact { get; set; }
    private string? SelectedArtifactName => Artifact?.Name;

    [Parameter] public bool IsWorking { get; set; }
    [Parameter] public string? FeedbackMessage { get; set; }
    [Parameter] public bool FeedbackIsError { get; set; }
    [Parameter] public EventCallback<UploadVersionInput> OnSubmit { get; set; }
    [Parameter] public EventCallback OnCancel { get; set; }

    public async Task OpenDialogAsync(string packageName)
    {
        PackageNameInput = packageName;
        IsPackageLocked = !string.IsNullOrWhiteSpace(packageName);
        Version = string.Empty;
        ChecksumSha256 = string.Empty;
        Artifact = null;
        IsHidden = false;
        await InvokeAsync(StateHasChanged);
    }

    public async Task CloseDialogAsync()
    {
        IsHidden = true;
        await OnCancel.InvokeAsync();
        await InvokeAsync(StateHasChanged);
    }

    private void HandleArtifactSelected(InputFileChangeEventArgs args)
    {
        Artifact = args.FileCount > 0 ? args.File : null;
    }

    private Task CloseAsync() => CloseDialogAsync();

    private Task SubmitAsync()
    {
        return OnSubmit.InvokeAsync(new UploadVersionInput(PackageNameInput, Version, ChecksumSha256, Artifact));
    }

    public sealed record UploadVersionInput(string? PackageName, string Version, string ChecksumSha256, IBrowserFile? Artifact);
}

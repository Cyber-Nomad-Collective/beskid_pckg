using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.FluentUI.AspNetCore.Components;

namespace Server.Components.Shared;

public partial class PackageVersionUploadDialog : IDialogContentComponent<PackageVersionUploadDialog.UploadVersionInput>
{
    [Parameter] public UploadVersionInput Content { get; set; } = new();
    [CascadingParameter] public FluentDialog Dialog { get; set; } = default!;

    private IBrowserFile? Artifact { get; set; }

    private void HandleArtifactSelected(InputFileChangeEventArgs args)
    {
        Artifact = args.FileCount > 0 ? args.File : null;
    }

    private Task CancelAsync() => Dialog.CancelAsync();

    private Task SubmitAsync() => Dialog.CloseAsync(Content with { Artifact = Artifact });

    public sealed record UploadVersionInput
    {
        public string? PackageName { get; set; }
        public string Version { get; set; } = string.Empty;
        public string ChecksumSha256 { get; set; } = string.Empty;
        public bool IsPackageLocked { get; set; }
        public IBrowserFile? Artifact { get; set; }
    }
}

using Microsoft.AspNetCore.Components;
using Microsoft.FluentUI.AspNetCore.Components;

namespace Server.Components.Shared;

public partial class PackageDocsExplorerDialog : IDialogContentComponent<PackageDocsExplorerDialog.ExplorerInput>
{
    [Parameter] public ExplorerInput Content { get; set; } = new();
    [CascadingParameter] public FluentDialog Dialog { get; set; } = default!;

    private Task CloseAsync() => Dialog.CancelAsync();

    public sealed record ExplorerInput
    {
        public string PackageName { get; init; } = string.Empty;
        public string Version { get; init; } = "latest";
    }
}

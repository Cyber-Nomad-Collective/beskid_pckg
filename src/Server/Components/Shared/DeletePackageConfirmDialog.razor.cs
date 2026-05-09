using Microsoft.AspNetCore.Components;
using Microsoft.FluentUI.AspNetCore.Components;

namespace Server.Components.Shared;

public partial class DeletePackageConfirmDialog : IDialogContentComponent<DeletePackageConfirmDialog.DeletePackageConfirmContent>
{
    [Parameter] public DeletePackageConfirmContent Content { get; set; } = new();
    [CascadingParameter] public FluentDialog Dialog { get; set; } = default!;

    private Task CancelAsync() => Dialog.CancelAsync();

    private Task ConfirmAsync() => Dialog.CloseAsync(true);

    public sealed class DeletePackageConfirmContent
    {
        public string PackageName { get; set; } = string.Empty;
    }
}

using Microsoft.AspNetCore.Components;
using Microsoft.FluentUI.AspNetCore.Components;
using Server.Features.Packages;

namespace Server.Components.Shared;

public partial class PackageGrid
{
    [Parameter] public IEnumerable<PackageSummaryResponse> Packages { get; set; } = [];
    [Parameter] public bool ShowActions { get; set; } = true;
    [Parameter] public bool ShowPackageDocsExplorerAction { get; set; }
    [Parameter] public EventCallback<PackageSummaryResponse> OnEditMetadata { get; set; }
    [Parameter] public EventCallback<PackageSummaryResponse> OnUploadVersion { get; set; }

    [Inject] private IDialogService DialogService { get; set; } = default!;

    private async Task OpenPackageDocsExplorerAsync(PackageSummaryResponse package)
    {
        var content = new PackageDocsExplorerDialog.ExplorerInput
        {
            PackageName = package.Name,
            Version = "latest"
        };

        var parameters = new DialogParameters
        {
            Width = "min(1024px, calc(100vw - 24px))",
            Modal = true,
            TrapFocus = true,
            PreventDismissOnOverlayClick = false
        };

        var dialog = await DialogService.ShowDialogAsync<PackageDocsExplorerDialog>(content, parameters);
        _ = await dialog.Result;
    }
}
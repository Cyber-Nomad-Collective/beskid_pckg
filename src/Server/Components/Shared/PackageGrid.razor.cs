using Microsoft.AspNetCore.Components;
using Microsoft.FluentUI.AspNetCore.Components;
using Icons = Microsoft.FluentUI.AspNetCore.Components.Icons;
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

    private IReadOnlyList<GridActionDefinition> GetPackageRowActions(PackageSummaryResponse package)
    {
        var list = new List<GridActionDefinition>();

        if (OnUploadVersion.HasDelegate)
        {
            list.Add(new GridActionDefinition
            {
                Icon = new Icons.Regular.Size20.ArrowUpload(),
                Tooltip = "Upload version",
                Appearance = Appearance.Accent,
                OnClick = EventCallback.Factory.Create(this, () => OnUploadVersion.InvokeAsync(package))
            });
        }

        if (OnEditMetadata.HasDelegate)
        {
            list.Add(new GridActionDefinition
            {
                Icon = new Icons.Regular.Size20.DocumentEdit(),
                Tooltip = "Edit metadata",
                OnClick = EventCallback.Factory.Create(this, () => OnEditMetadata.InvokeAsync(package))
            });
        }

        if (ShowPackageDocsExplorerAction)
        {
            list.Add(new GridActionDefinition
            {
                Icon = new Icons.Regular.Size20.DocumentSearch(),
                Tooltip = "Browse docs",
                OnClick = EventCallback.Factory.Create(this, () => OpenPackageDocsExplorerAsync(package))
            });
        }

        return list;
    }
}
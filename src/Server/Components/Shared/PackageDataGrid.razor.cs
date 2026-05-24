using Microsoft.AspNetCore.Components;
using Microsoft.FluentUI.AspNetCore.Components;
using Server.Features.Packages;

namespace Server.Components.Shared;

public partial class PackageDataGrid
{
    [Parameter] public IEnumerable<PackageSummaryResponse> Packages { get; set; } = [];
    [Parameter] public bool ShowActions { get; set; } = true;
    [Parameter] public EventCallback<PackageSummaryResponse> OnEditMetadata { get; set; }
    [Parameter] public EventCallback<PackageSummaryResponse> OnUploadVersion { get; set; }
    [Parameter] public EventCallback<PackageSummaryResponse> OnDeletePackage { get; set; }

    private IReadOnlyList<GridActionDefinition> GetPackageRowActions(PackageSummaryResponse package)
    {
        var list = new List<GridActionDefinition>();

        if (OnUploadVersion.HasDelegate)
        {
            list.Add(new GridActionDefinition
            {
                Icon = new Microsoft.FluentUI.AspNetCore.Components.Icons.Regular.Size20.ArrowUpload(),
                Tooltip = "Upload version",
                Appearance = Appearance.Accent,
                OnClick = EventCallback.Factory.Create(this, () => OnUploadVersion.InvokeAsync(package))
            });
        }

        if (OnEditMetadata.HasDelegate)
        {
            list.Add(new GridActionDefinition
            {
                Icon = new Microsoft.FluentUI.AspNetCore.Components.Icons.Regular.Size20.DocumentEdit(),
                Tooltip = "Edit metadata",
                OnClick = EventCallback.Factory.Create(this, () => OnEditMetadata.InvokeAsync(package))
            });
        }

        if (OnDeletePackage.HasDelegate)
        {
            list.Add(new GridActionDefinition
            {
                Icon = new Microsoft.FluentUI.AspNetCore.Components.Icons.Regular.Size20.Delete(),
                Tooltip = "Delete package",
                Appearance = Appearance.Outline,
                OnClick = EventCallback.Factory.Create(this, () => OnDeletePackage.InvokeAsync(package))
            });
        }

        return list;
    }
}
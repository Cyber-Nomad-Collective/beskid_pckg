using Microsoft.AspNetCore.Components;
using pckg.Features.Packages;

namespace Server.Components.Shared;

public partial class PackageGrid
{
    [Parameter] public IEnumerable<PackageSummaryResponse> Packages { get; set; } = [];
    [Parameter] public bool ShowActions { get; set; } = true;
    [Parameter] public EventCallback<PackageSummaryResponse> OnEditMetadata { get; set; }
    [Parameter] public EventCallback<PackageSummaryResponse> OnUploadVersion { get; set; }
}
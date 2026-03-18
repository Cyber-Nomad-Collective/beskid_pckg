using Microsoft.FluentUI.AspNetCore.Components;
using Microsoft.AspNetCore.Components;

namespace Server.Components.Layout;

public partial class DashboardLayout
{
    [Inject] private IHttpContextAccessor HttpContextAccessor { get; set; } = default!;
    private bool IsSuperAdmin => HttpContextAccessor.HttpContext?.User?.IsInRole("SuperAdmin") == true;
    private const int SideMenuWidth = 230;

    private static readonly Icon ProfileIcon =
        new Microsoft.FluentUI.AspNetCore.Components.Icons.Regular.Size20.Person();

    private static readonly Icon ApiKeysIcon = new Microsoft.FluentUI.AspNetCore.Components.Icons.Regular.Size20.Key();
    private static readonly Icon PackagesIcon = new Microsoft.FluentUI.AspNetCore.Components.Icons.Regular.Size20.Box();

    private static readonly Icon MyPackagesIcon =
        new Microsoft.FluentUI.AspNetCore.Components.Icons.Regular.Size20.Folder();

    private static readonly Icon VersionsIcon =
        new Microsoft.FluentUI.AspNetCore.Components.Icons.Regular.Size20.Folder();

    private static readonly Icon AdminPackagesIcon =
        new Microsoft.FluentUI.AspNetCore.Components.Icons.Regular.Size20.Building();

    private static readonly Icon UploadIcon = new Microsoft.FluentUI.AspNetCore.Components.Icons.Regular.Size20.ArrowUpload();
    private static readonly Icon HomeIcon = new Microsoft.FluentUI.AspNetCore.Components.Icons.Regular.Size20.Home();
}
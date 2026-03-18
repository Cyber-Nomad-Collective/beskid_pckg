using Microsoft.FluentUI.AspNetCore.Components;

namespace Server.Components.Layout;

public partial class DashboardLayout
{
    private bool IsSidebarOpen { get; set; } = true;
    private string SidebarClass => $"dashboard-sidebar-shell {(IsSidebarOpen ? "open" : "collapsed")}";
    private const int SideMenuWidth = 230;
    private void ToggleSidebar()
    {
        IsSidebarOpen = !IsSidebarOpen;
        StateHasChanged();
    }

    private void CloseSidebar() => IsSidebarOpen = false;

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
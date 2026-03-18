using Microsoft.AspNetCore.Components;
using Microsoft.FluentUI.AspNetCore.Components;

namespace Server.Components.Layout;

public partial class UserMenu
{
    [Parameter] public string DisplayName { get; set; } = "Account";
    
    [Parameter] public bool IsOpen { get; set; }
    [Parameter] public EventCallback<bool> IsOpenChanged { get; set; }

    private static readonly Icon ApiKeysIcon = new Microsoft.FluentUI.AspNetCore.Components.Icons.Regular.Size16.Key();
    private static readonly Icon LogoutIcon = new Microsoft.FluentUI.AspNetCore.Components.Icons.Regular.Size20.SignOut();
    private static readonly Icon PackagesIcon = new Microsoft.FluentUI.AspNetCore.Components.Icons.Regular.Size16.Box();
    private static readonly Icon ProfileIcon = new Microsoft.FluentUI.AspNetCore.Components.Icons.Regular.Size16.Person();

    private string GetInitials()
    {
        if (string.IsNullOrWhiteSpace(DisplayName)) return "U";

        var parts = DisplayName.Split('@', StringSplitOptions.RemoveEmptyEntries);
        var name = parts.Length > 0 ? parts[0] : DisplayName;

        return name.Length > 1
            ? name.Substring(0, 2).ToUpperInvariant()
            : name.Substring(0, 1).ToUpperInvariant();
    }

    private void NavigateTo(string url)
    {
        Navigation.NavigateTo(url);
    }

    private void Logout()
    {
        Navigation.NavigateTo("/users/logout?returnUrl=%2Fauth%3Fmode%3Dlogin", forceLoad: true);
    }
}
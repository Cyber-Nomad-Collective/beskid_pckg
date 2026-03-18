using Microsoft.FluentUI.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Server.Components.Layout;

public partial class ThemeToggle
{
    private static readonly Icon SunIcon =
        new Microsoft.FluentUI.AspNetCore.Components.Icons.Filled.Size20.WeatherSunny();

    private static readonly Icon MoonIcon =
        new Microsoft.FluentUI.AspNetCore.Components.Icons.Filled.Size20.WeatherMoon();

    private bool IsDark { get; set; }
    private Icon CurrentIcon => IsDark ? SunIcon : MoonIcon;
    private string ButtonLabel => IsDark ? "Switch to light mode" : "Switch to dark mode";
    private IJSObjectReference? ThemeModule;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender || ThemeModule is not null) return;

        try
        {
            ThemeModule = await JS.InvokeAsync<IJSObjectReference>("import", "/theme-manager.js");
            var mode = await ThemeModule.InvokeAsync<string>("getInitialThemeMode");
            var isDark = mode == "dark";
            if (IsDark != isDark)
            {
                IsDark = isDark;
                StateHasChanged();
            }
        }
        catch (InvalidOperationException)
        {
        }
    }

    private async Task HandleToggle()
    {
        if (ThemeModule is null) return;

        var mode = await ThemeModule.InvokeAsync<string>("toggleThemeMode", IsDark ? "dark" : "light");
        IsDark = mode == "dark";
        StateHasChanged();
    }

    public async ValueTask DisposeAsync()
    {
        if (ThemeModule is not null)
        {
            try
            {
                await ThemeModule.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
            }
            catch (TaskCanceledException)
            {
            }
        }
    }
}
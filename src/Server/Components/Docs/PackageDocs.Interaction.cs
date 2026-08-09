using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using Server.Contracts.ApiDocumentation;

namespace Server.Components.Docs;

public partial class PackageDocs
{
    private void ToggleMorePanel()
    {
        _showMorePanel = !_showMorePanel;
        if (_showMorePanel)
        {
            _ = PositionFilterPopoverAsync();
        }
    }

    private void CloseMorePanel() => _showMorePanel = false;

    private async Task SelectSymbolFromPanelAsync(StructuredApiItemDto item)
    {
        await SelectItemAsync(item);
        CloseMorePanel();
    }

    private async Task PositionFilterPopoverAsync()
    {
        try
        {
            await Js.InvokeVoidAsync("pckgDocs.positionFilterPopover");
        }
        catch
        {
            // Ignore when JS interop is unavailable during prerender.
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_showMorePanel)
        {
            await PositionFilterPopoverAsync();
        }
    }

    private async Task HandleDocsKeyDown(KeyboardEventArgs e)
    {
        if (
            !string.Equals(e.Key, "/", StringComparison.Ordinal)
            || e.CtrlKey
            || e.MetaKey
            || e.AltKey
        )
        {
            return;
        }

        try
        {
            await Js.InvokeVoidAsync("pckgDocs.focusSymbolSearch");
        }
        catch
        {
            // Ignore when JS interop is unavailable during prerender.
        }
    }
}

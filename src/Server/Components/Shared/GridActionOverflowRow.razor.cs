using Microsoft.AspNetCore.Components;

namespace Server.Components.Shared;

/// <summary>
/// Row actions for data grids: one action renders inline; multiple actions use a FluentMenu anchored to a square icon button.
/// </summary>
public partial class GridActionOverflowRow
{
    private bool _menuOpen;
    private readonly string _menuButtonId = $"grid-actions-{Guid.NewGuid():N}";

    [Parameter] public IReadOnlyList<GridActionDefinition>? Actions { get; set; }

    [Parameter] public string? Class { get; set; }

    private Task ToggleMenuAsync()
    {
        _menuOpen = !_menuOpen;
        return Task.CompletedTask;
    }

    private async Task InvokeFromMenuAsync(GridActionDefinition def)
    {
        _menuOpen = false;
        await def.OnClick.InvokeAsync();
    }
}

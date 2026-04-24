using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using Microsoft.FluentUI.AspNetCore.Components;

namespace Server.Components.Shared;

/// <summary>
/// Row actions with FluentOverflow; overflow menu resolves items by list index (hidden in each item innerText as digits), not by tooltip text.
/// Keep <see cref="Actions"/> order stable for the lifetime of the row UI.
/// </summary>
public partial class GridActionOverflowRow
{
    private static readonly Regex s_leadingActionIndex = new(@"^(\d+)", RegexOptions.CultureInvariant);

    private bool _menuOpen;

    [Parameter] public IReadOnlyList<GridActionDefinition>? Actions { get; set; }

    [Parameter] public string? Class { get; set; }

    private Task OnOverflowRaisedAsync(IEnumerable<FluentOverflowItem> _)
    {
        StateHasChanged();
        return Task.CompletedTask;
    }

    private GridActionDefinition? ResolveDefinition(FluentOverflowItem item)
    {
        if (Actions is null || Actions.Count == 0)
        {
            return null;
        }

        var raw = item.Text?.Trim() ?? string.Empty;
        var match = s_leadingActionIndex.Match(raw);
        if (!match.Success
            || !int.TryParse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var index)
            || index < 0
            || index >= Actions.Count)
        {
            return null;
        }

        return Actions[index];
    }

    private async Task InvokeFromMenuAsync(GridActionDefinition def)
    {
        _menuOpen = false;
        await def.OnClick.InvokeAsync();
    }
}

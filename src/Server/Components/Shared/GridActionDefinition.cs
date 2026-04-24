using Microsoft.AspNetCore.Components;
using Microsoft.FluentUI.AspNetCore.Components;

namespace Server.Components.Shared;

/// <summary>One icon action for <see cref="GridActionOverflowRow"/>.</summary>
public sealed class GridActionDefinition
{
    public required Icon Icon { get; init; }

    /// <summary>Visible label in the overflow menu and primary action tooltip.</summary>
    public required string Tooltip { get; init; }

    public EventCallback OnClick { get; init; }

    public Appearance Appearance { get; init; } = Appearance.Outline;

    public bool Disabled { get; init; }
}

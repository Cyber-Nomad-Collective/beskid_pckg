using Microsoft.AspNetCore.Components;
using Microsoft.FluentUI.AspNetCore.Components;

namespace Server.Components.Shared;

public partial class GridActionButton
{
    private const string SquareIconButtonStyle =
        "width: 40px; height: 40px; min-width: 40px; padding: 0; box-sizing: border-box;";

    /// <summary>When set, replaces the default square icon layout (use sparingly).</summary>
    [Parameter] public string? HostStyle { get; set; }

    [Parameter, EditorRequired] public Icon Icon { get; set; } = default!;

    [Parameter, EditorRequired] public string Tooltip { get; set; } = "";

    /// <summary>DOM id for the host <c>fluent-button</c> (e.g. FluentOverflow <c>IdMoreButton</c> anchor).</summary>
    [Parameter] public string? Id { get; set; }

    [Parameter] public Appearance Appearance { get; set; } = Appearance.Outline;

    [Parameter] public bool Disabled { get; set; }

    [Parameter] public bool StopPropagation { get; set; }

    [Parameter] public EventCallback OnClick { get; set; }

    private string ResolvedHostStyle => string.IsNullOrEmpty(HostStyle) ? SquareIconButtonStyle : HostStyle!;
}

using Microsoft.AspNetCore.Components;
using Server.Data;

namespace Server.Components.Shared;

/// <summary>
/// Thread-type picker for the board composer. Kept separate so changing the selection
/// does not re-render the rest of the composer (rich text editor, captcha, etc.).
/// </summary>
public partial class BoardComposerPostTypeTiles
{
    [Parameter]
    public BoardPostType SelectedPostType { get; set; }

    [Parameter]
    public EventCallback<BoardPostType> SelectedPostTypeChanged { get; set; }

    private bool _hasSelectionSnapshot;
    private BoardPostType _selectionSnapshot;

    protected override bool ShouldRender()
    {
        if (_hasSelectionSnapshot && _selectionSnapshot == SelectedPostType)
        {
            return false;
        }

        _hasSelectionSnapshot = true;
        _selectionSnapshot = SelectedPostType;
        return true;
    }

    private Task SelectAsync(BoardPostType type)
        => SelectedPostTypeChanged.InvokeAsync(type);

    private static string GetPostTypeColor(BoardPostType type) => type switch
    {
        BoardPostType.Issue => "#dc3545",
        BoardPostType.FeatureRequest => "#6f42c1",
        BoardPostType.Suggestion => "#198754",
        _ => "#6c757d"
    };
}

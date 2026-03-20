using Microsoft.AspNetCore.Components;

namespace Server.Components.Shared;

public partial class FluentRatingPicker
{
    [Parameter] public int Value { get; set; } = 5;
    [Parameter] public EventCallback<int> ValueChanged { get; set; }
    [Parameter] public int Max { get; set; } = 5;
    [Parameter] public bool ReadOnly { get; set; }

    private async Task SelectAsync(int value)
    {
        if (ReadOnly)
        {
            return;
        }

        var normalized = Math.Clamp(value, 1, Max <= 0 ? 5 : Max);
        Value = normalized;
        await ValueChanged.InvokeAsync(normalized);
    }
}

using Blazored.TextEditor;
using Microsoft.AspNetCore.Components;

namespace Server.Components.Shared;

public partial class ThemedRichTextEditor
{
    [Parameter] public string Label { get; set; } = string.Empty;
    [Parameter] public string Placeholder { get; set; } = "Write your message...";
    [Parameter] public string HintText { get; set; } = string.Empty;
    [Parameter] public string Class { get; set; } = string.Empty;
    [Parameter] public bool Disabled { get; set; }
    [Parameter] public string Value { get; set; } = string.Empty;
    [Parameter] public EventCallback<string> ValueChanged { get; set; }

    private BlazoredTextEditor? Editor { get; set; }
    private string LastSyncedHtml { get; set; } = string.Empty;
    private bool PendingHtmlLoad { get; set; } = true;

    protected override void OnParametersSet()
    {
        var normalizedIncoming = NormalizeHtml(Value);
        if (!string.Equals(normalizedIncoming, LastSyncedHtml, StringComparison.Ordinal))
        {
            PendingHtmlLoad = true;
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!PendingHtmlLoad || Editor is null)
        {
            return;
        }

        var normalizedIncoming = NormalizeHtml(Value);
        await Editor.LoadHTMLContent(normalizedIncoming);
        LastSyncedHtml = normalizedIncoming;
        PendingHtmlLoad = false;
    }

    public async Task<string> GetHtmlAsync()
    {
        if (Editor is null)
        {
            return NormalizeHtml(Value);
        }

        return NormalizeHtml(await Editor.GetHTML());
    }

    public async Task SyncToBoundValueAsync()
    {
        if (!ValueChanged.HasDelegate)
        {
            return;
        }

        var html = await GetHtmlAsync();
        if (string.Equals(html, LastSyncedHtml, StringComparison.Ordinal))
        {
            return;
        }

        LastSyncedHtml = html;
        await ValueChanged.InvokeAsync(html);
    }

    private static string NormalizeHtml(string? html)
    {
        if (string.IsNullOrWhiteSpace(html) || string.Equals(html.Trim(), "<p><br></p>", StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        return html.Trim();
    }
}

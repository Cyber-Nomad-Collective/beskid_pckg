using Blazored.TextEditor;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

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

    /// <summary>
    /// Skip DOM updates when only unrelated parent state changed (e.g. thread type tiles above the editor).
    /// That avoids tearing down / racing Blazored Quill while <c>LoadHTMLContent</c> expects <c>__quill</c> on the element.
    /// </summary>
    private RenderSnapshot? _lastRenderSnapshot;

    private readonly record struct RenderSnapshot(
        string Label,
        string Placeholder,
        string HintText,
        string Class,
        bool Disabled,
        string Value);

    protected override bool ShouldRender()
    {
        var snapshot = new RenderSnapshot(
            Label,
            Placeholder,
            HintText,
            Class,
            Disabled,
            Value);

        if (_lastRenderSnapshot is { } last && last == snapshot)
        {
            return false;
        }

        _lastRenderSnapshot = snapshot;
        return true;
    }

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
        await TryLoadHtmlIntoEditorAsync(normalizedIncoming);
    }

    /// <summary>
    /// Blazored creates Quill in its own <see cref="ComponentBase.OnAfterRenderAsync"/>; our wrapper can run in the same
    /// frame and call <see cref="BlazoredTextEditor.LoadHTMLContent"/> before <c>quillElement.__quill</c> exists.
    /// Retrying after short delays covers that race without requiring upstream components to avoid re-renders.
    /// </summary>
    private async Task TryLoadHtmlIntoEditorAsync(string normalizedIncoming)
    {
        if (Editor is null)
        {
            return;
        }

        const int maxAttempts = 10;
        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            try
            {
                await Editor.LoadHTMLContent(normalizedIncoming);
                LastSyncedHtml = normalizedIncoming;
                PendingHtmlLoad = false;
                return;
            }
            catch (JSException) when (attempt < maxAttempts - 1)
            {
                await Task.Delay(16 * (attempt + 1));
            }
        }

        // Avoid killing the Blazor circuit if Quill never becomes ready (misconfigured scripts, blocked CDN, etc.).
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

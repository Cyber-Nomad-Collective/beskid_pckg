using System.Net.Http.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.FluentUI.AspNetCore.Components;
using Icons = Microsoft.FluentUI.AspNetCore.Components.Icons;
using Server.Components.Shared;

namespace Server.Components.Pages.Dashboard.Admin;

public partial class AdminBlockedLinks
{
    private List<BlockedLinkRowDto> BlockedLinks = [];
    private IQueryable<BlockedLinkRowDto> BlockedLinksQueryable => BlockedLinks.AsQueryable();
    private bool IsLoadingBlockedLinks = true;
    private bool IsSavingLinks;
    private string? FeedbackMessage;
    private MessageIntent? FeedbackIntent;
    private string NewBlockedPattern = string.Empty;
    private string NewBlockedNote = string.Empty;

    [Inject]
    public HttpClient ApiHttp { get; set; } = default!;

    protected override async Task OnInitializedAsync() => await LoadBlockedLinksAsync();

    private async Task LoadBlockedLinksAsync()
    {
        IsLoadingBlockedLinks = true;
        try
        {
            var response = await ApiHttp.GetAsync("/api/admin/blocked-links");
            if (!response.IsSuccessStatusCode)
            {
                SetFeedback("Failed to load blocked link patterns.", MessageIntent.Error);
                return;
            }

            var rows = await response.Content.ReadFromJsonAsync<List<BlockedLinkRowDto>>();
            BlockedLinks = rows ?? [];
        }
        finally
        {
            IsLoadingBlockedLinks = false;
        }
    }

    private async Task AddBlockedLinkAsync()
    {
        var pattern = NewBlockedPattern.Trim();
        if (string.IsNullOrWhiteSpace(pattern))
        {
            SetFeedback("Enter a URL substring to block.", MessageIntent.Warning);
            return;
        }

        IsSavingLinks = true;
        try
        {
            var response = await ApiHttp.PostAsJsonAsync(
                "/api/admin/blocked-links",
                new AddBlockedLinkApiRequest(pattern, string.IsNullOrWhiteSpace(NewBlockedNote) ? null : NewBlockedNote.Trim()));

            if (!response.IsSuccessStatusCode)
            {
                var err = await response.Content.ReadFromJsonAsync<AddBlockedLinkApiResponse>();
                SetFeedback(err?.Message ?? "Failed to add pattern.", MessageIntent.Error);
                return;
            }

            var body = await response.Content.ReadFromJsonAsync<AddBlockedLinkApiResponse>();
            SetFeedback(body?.Message ?? "Pattern added.", MessageIntent.Success);
            NewBlockedPattern = string.Empty;
            NewBlockedNote = string.Empty;
            await LoadBlockedLinksAsync();
        }
        finally
        {
            IsSavingLinks = false;
        }
    }

    private IReadOnlyList<GridActionDefinition> GetBlockedLinkRowActions(BlockedLinkRowDto row) =>
    [
        new GridActionDefinition
        {
            Icon = new Icons.Regular.Size20.Delete(),
            Tooltip = "Remove blocked pattern",
            Disabled = IsSavingLinks,
            OnClick = EventCallback.Factory.Create(this, () => DeleteBlockedLinkAsync(row.Id))
        }
    ];

    private async Task DeleteBlockedLinkAsync(Guid id)
    {
        IsSavingLinks = true;
        try
        {
            var response = await ApiHttp.DeleteAsync($"/api/admin/blocked-links/{id}");
            if (!response.IsSuccessStatusCode)
            {
                SetFeedback("Failed to remove pattern.", MessageIntent.Error);
                return;
            }

            SetFeedback("Pattern removed.", MessageIntent.Success);
            await LoadBlockedLinksAsync();
        }
        finally
        {
            IsSavingLinks = false;
        }
    }

    private void SetFeedback(string message, MessageIntent intent)
    {
        FeedbackMessage = message;
        FeedbackIntent = intent;
    }
}

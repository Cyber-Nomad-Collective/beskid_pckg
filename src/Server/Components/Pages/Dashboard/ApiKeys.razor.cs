using Microsoft.AspNetCore.Components;
using Microsoft.FluentUI.AspNetCore.Components;
using Microsoft.JSInterop;
using Server.Components.Shared;
using Server.Features.ApiKeys;

namespace Server.Components.Pages.Dashboard;

public partial class ApiKeys
{
    private readonly List<ApiKeysListResponse> ApiKeyItems = [];
    private string? LastCreatedKey;
    private string? FeedbackMessage;
    private bool FeedbackIsError;
    private bool IsWorking;

    [Inject]
    public IDialogService DialogService { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        await LoadKeysAsync();
    }

    private async Task LoadKeysAsync()
    {
        ApiKeyItems.Clear();

        var response = await Http.GetAsync("/api/keys");
        if (!response.IsSuccessStatusCode)
        {
            SetFeedback("Unable to load API keys.", isError: true);
            return;
        }

        var rows = await response.Content.ReadFromJsonAsync<List<ApiKeysListResponse>>() ?? [];

        ApiKeyItems.AddRange(rows);
    }

    private async Task CreateApiKeyAsync(ApiKeyGenerateDialog.CreateKeyInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Name))
        {
            SetFeedback("Key name is required.", isError: true);
            return;
        }

        IsWorking = true;
        try
        {
            var scopes = new List<string>();
            if (input.PublishScope)
            {
                scopes.Add("publish");
            }

            if (input.ReadScope)
            {
                scopes.Add("read");
            }

            var response = await Http.PostAsJsonAsync("/api/keys",
                new CreateApiKeyRequest(input.Name.Trim(), scopes.ToArray()));
            var payload = await response.Content.ReadFromJsonAsync<CreateApiKeyResponse>();

            if (!response.IsSuccessStatusCode || payload is null || !payload.Success)
            {
                SetFeedback(payload?.Message ?? "Unable to create API key.", isError: true);
                return;
            }

            LastCreatedKey = payload.PlainTextKey;
            await LoadKeysAsync();
            SetFeedback(payload.Message, isError: false);
        }
        finally
        {
            IsWorking = false;
        }
    }

    private async Task RevokeApiKeyAsync(Guid keyId)
    {
        IsWorking = true;
        try
        {
            var response = await Http.PostAsync($"/api/keys/{keyId:D}/revoke", content: null);
            var payload = await response.Content.ReadFromJsonAsync<RevokeApiKeyResponse>();

            if (!response.IsSuccessStatusCode || payload is null || !payload.Success)
            {
                SetFeedback(payload?.Message ?? "Unable to revoke API key.", isError: true);
                return;
            }

            await LoadKeysAsync();
            SetFeedback(payload.Message, isError: false);
        }
        finally
        {
            IsWorking = false;
        }
    }

    private async Task CopyLastKeyAsync()
    {
        if (string.IsNullOrWhiteSpace(LastCreatedKey))
        {
            return;
        }

        await JsRuntime.InvokeVoidAsync("navigator.clipboard.writeText", LastCreatedKey);
        SetFeedback("API key copied to clipboard.", isError: false);
    }

    private void ClearLastKey() => LastCreatedKey = null;

    private async Task OpenGenerateKeyDialogAsync()
    {
        var content = new ApiKeyGenerateDialog.CreateKeyInput
        {
            Name = string.Empty,
            PublishScope = true,
            ReadScope = true
        };

        var parameters = new DialogParameters
        {
            Width = "min(620px, calc(100vw - 32px))",
            Modal = true,
            TrapFocus = true,
            PreventDismissOnOverlayClick = true
        };

        var dialog = await DialogService.ShowDialogAsync<ApiKeyGenerateDialog>(content, parameters);
        var result = await dialog.Result;

        if (result?.Cancelled != false || result.Data is not ApiKeyGenerateDialog.CreateKeyInput input)
        {
            return;
        }

        await CreateApiKeyAsync(input);
    }

    private IReadOnlyList<GridActionDefinition> GetApiKeyRowActions(ApiKeysListResponse key)
    {
        if (key.RevokedAtUtc is not null)
        {
            return Array.Empty<GridActionDefinition>();
        }

        return
        [
            new GridActionDefinition
            {
                Icon = new Microsoft.FluentUI.AspNetCore.Components.Icons.Regular.Size20.DismissCircle(),
                Tooltip = "Revoke API key",
                Disabled = IsWorking,
                OnClick = EventCallback.Factory.Create(this, () => RevokeApiKeyAsync(key.Id))
            }
        ];
    }

    private void SetFeedback(string message, bool isError)
    {
        FeedbackMessage = message;
        FeedbackIsError = isError;
    }

    private sealed record CreateApiKeyRequest(string Name, string[] Scopes);
    
    private sealed record CreateApiKeyResponse(bool Success, string? PlainTextKey, ApiKeyView? Key, string Message);

    private sealed record RevokeApiKeyResponse(bool Success, string Message, DateTimeOffset? RevokedAtUtc);
}
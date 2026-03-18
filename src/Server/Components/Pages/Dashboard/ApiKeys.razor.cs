using Microsoft.JSInterop;

namespace Server.Components.Pages.Dashboard;

public partial class ApiKeys
{
    private readonly List<ApiKeyRow> ApiKeyItems = [];
    private readonly CreateKeyModel CreateModel = new();
    private string? LastCreatedKey;
    private string? FeedbackMessage;
    private bool FeedbackIsError;
    private bool IsWorking;

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

        var rows = await response.Content.ReadFromJsonAsync<List<ApiKeyRow>>() ?? [];

        ApiKeyItems.AddRange(rows);
    }

    private async Task CreateApiKeyAsync()
    {
        if (string.IsNullOrWhiteSpace(CreateModel.Name))
        {
            SetFeedback("Key name is required.", isError: true);
            return;
        }

        IsWorking = true;
        try
        {
            var scopes = new List<string>();
            if (CreateModel.PublishScope)
            {
                scopes.Add("publish");
            }

            if (CreateModel.ReadScope)
            {
                scopes.Add("read");
            }

            var response = await Http.PostAsJsonAsync("/api/keys",
                new CreateApiKeyRequest(CreateModel.Name.Trim(), scopes.ToArray()));
            var payload = await response.Content.ReadFromJsonAsync<CreateApiKeyResponse>();

            if (!response.IsSuccessStatusCode || payload is null || !payload.Success)
            {
                SetFeedback(payload?.Message ?? "Unable to create API key.", isError: true);
                return;
            }

            LastCreatedKey = payload.PlainTextKey;
            CreateModel.Name = string.Empty;
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

    private void ClearLastKey()
    {
        LastCreatedKey = null;
    }

    private void SetFeedback(string message, bool isError)
    {
        FeedbackMessage = message;
        FeedbackIsError = isError;
    }

    private sealed record ApiKeyRow(
        Guid Id,
        string Name,
        string Prefix,
        IReadOnlyList<string> Scopes,
        DateTimeOffset CreatedAtUtc,
        DateTimeOffset? RevokedAtUtc);

    private sealed record CreateApiKeyRequest(string Name, string[] Scopes);

    private sealed record CreateApiKeyResponse(bool Success, string? PlainTextKey, ApiKeyRow? Key, string Message);

    private sealed record RevokeApiKeyResponse(bool Success, string Message, DateTimeOffset? RevokedAtUtc);

    private sealed class CreateKeyModel
    {
        public string Name { get; set; } = string.Empty;
        public bool PublishScope { get; set; } = true;
        public bool ReadScope { get; set; } = true;
    }
}
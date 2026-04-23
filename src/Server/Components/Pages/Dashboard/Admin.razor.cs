using Microsoft.AspNetCore.Components;
using System.Net.Http.Json;

namespace Server.Components.Pages.Dashboard;

public partial class Admin : IDisposable
{
    private string AdminTabId = "admin-tab-users";
    private List<UserDto> Users = [];
    private IQueryable<UserDto> UsersQueryable => Users.AsQueryable();
    private bool IsLoading = true;
    private bool IsSaving;
    private string? FeedbackMessage;
    private bool FeedbackIsError;
    private string SearchQuery = string.Empty;
    private int CurrentPage = 1;
    private int PageSize = 50;
    private int TotalCount;
    private int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    private bool IsEditDialogOpen;
    private UserDto? SelectedUser;
    private List<string> EditUserRoles = [];
    private readonly List<string> AvailableRoles = ["User", "SuperAdmin", "Moderator"];

    [Inject]
    public HttpClient ApiHttp { get; set; } = default!;

    private List<BlockedLinkRowDto> BlockedLinks = [];
    private IQueryable<BlockedLinkRowDto> BlockedLinksQueryable => BlockedLinks.AsQueryable();
    private bool IsLoadingBlockedLinks = true;
    private bool IsSavingLinks;
    private string NewBlockedPattern = string.Empty;
    private string NewBlockedNote = string.Empty;

    private List<RegistryActivityRowDto> RegistryActivity = [];
    private bool IsLoadingRegistryActivity;
    private System.Threading.Timer? _registryActivityTimer;

    protected override async Task OnInitializedAsync()
    {
        await LoadUsersAsync();
        await LoadEmailSettingsAsync();
        await LoadBlockedLinksAsync();

        _registryActivityTimer = new Timer(
            _ => _ = InvokeAsync(PollRegistryActivityIfNeededAsync),
            null,
            TimeSpan.FromSeconds(3),
            TimeSpan.FromSeconds(3));
    }

    public void Dispose()
    {
        _registryActivityTimer?.Dispose();
    }

    private async Task PollRegistryActivityIfNeededAsync()
    {
        if (AdminTabId != "admin-tab-registry-activity")
        {
            return;
        }

        await LoadRegistryActivityAsync();
        StateHasChanged();
    }

    private Task RefreshRegistryActivityAsync() => LoadRegistryActivityAsync();

    private async Task LoadRegistryActivityAsync()
    {
        IsLoadingRegistryActivity = true;
        try
        {
            var response = await ApiHttp.GetAsync("/api/admin/registry-activity?take=200");
            if (!response.IsSuccessStatusCode)
            {
                return;
            }

            var rows = await response.Content.ReadFromJsonAsync<List<RegistryActivityRowDto>>();
            RegistryActivity = rows ?? [];
        }
        finally
        {
            IsLoadingRegistryActivity = false;
        }
    }

    private async Task LoadUsersAsync()
    {
        IsLoading = true;
        try
        {
            var url = $"/api/admin/users?page={CurrentPage}&pageSize={PageSize}";
            if (!string.IsNullOrWhiteSpace(SearchQuery))
            {
                url += $"&search={Uri.EscapeDataString(SearchQuery)}";
            }

            var response = await ApiHttp.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                SetFeedback("Failed to load users.", true);
                return;
            }

            var result = await response.Content.ReadFromJsonAsync<ListUsersResponse>();
            if (result is not null)
            {
                Users = result.Users;
                TotalCount = result.TotalCount;
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task SearchUsersAsync()
    {
        CurrentPage = 1;
        await LoadUsersAsync();
    }

    private async Task PreviousPageAsync()
    {
        if (CurrentPage > 1)
        {
            CurrentPage--;
            await LoadUsersAsync();
        }
    }

    private async Task NextPageAsync()
    {
        if (CurrentPage < TotalPages)
        {
            CurrentPage++;
            await LoadUsersAsync();
        }
    }

    private void OpenEditUserDialog(UserDto user)
    {
        SelectedUser = user;
        EditUserRoles = new List<string>(user.Roles);
        IsEditDialogOpen = true;
    }

    private void CloseEditDialog()
    {
        IsEditDialogOpen = false;
        SelectedUser = null;
        EditUserRoles.Clear();
    }

    private void ToggleRole(string role, bool isChecked)
    {
        if (isChecked && !EditUserRoles.Contains(role))
        {
            EditUserRoles.Add(role);
        }
        else if (!isChecked && EditUserRoles.Contains(role))
        {
            EditUserRoles.Remove(role);
        }
    }

    private async Task SaveUserRolesAsync()
    {
        if (SelectedUser is null) return;

        IsSaving = true;
        try
        {
            var response = await ApiHttp.PutAsJsonAsync(
                $"/api/admin/users/{SelectedUser.Id}/roles",
                new UpdateUserRolesRequest(EditUserRoles));

            if (!response.IsSuccessStatusCode)
            {
                SetFeedback("Failed to update user roles.", true);
                return;
            }

            var result = await response.Content.ReadFromJsonAsync<UpdateUserRolesResponse>();
            if (result?.Success == true)
            {
                SetFeedback("User roles updated successfully.", false);
                CloseEditDialog();
                await LoadUsersAsync();
            }
            else
            {
                SetFeedback(result?.Message ?? "Failed to update user roles.", true);
            }
        }
        finally
        {
            IsSaving = false;
        }
    }

    private void SetFeedback(string message, bool isError)
    {
        FeedbackMessage = message;
        FeedbackIsError = isError;
    }

    // Email settings
    private EmailSettingsModel EmailSettings = new();
    private async Task LoadEmailSettingsAsync()
    {
        try
        {
            var resp = await ApiHttp.GetAsync("/api/admin/email-settings");
            if (!resp.IsSuccessStatusCode) return;
            var payload = await resp.Content.ReadFromJsonAsync<GetEmailSettingsResponse>();
            if (payload is null) return;
            EmailSettings = new EmailSettingsModel
            {
                SmtpHost = payload.SmtpHost,
                SmtpPort = payload.SmtpPort,
                EnableSsl = payload.EnableSsl,
                Username = payload.Username,
                Password = payload.Password ?? string.Empty,
                FromEmail = payload.FromEmail,
                FromName = payload.FromName
            };
        }
        catch { }
    }

    private async Task SaveEmailSettingsAsync()
    {
        IsSaving = true;
        try
        {
            await ApiHttp.PostAsJsonAsync("/api/admin/email-settings", EmailSettings);
            SetFeedback("Email settings saved.", false);
        }
        finally
        {
            IsSaving = false;
        }
    }

    private sealed class EmailSettingsModel
    {
        public string? SmtpHost { get; set; }
        public int SmtpPort { get; set; } = 587;
        public bool EnableSsl { get; set; } = true;
        public string? Username { get; set; }
        public string Password { get; set; } = string.Empty;
        public string FromEmail { get; set; } = "no-reply@beskid";
        public string FromName { get; set; } = "Beskid Pckg";
    }

    private sealed record GetEmailSettingsResponse(string? SmtpHost, int SmtpPort, bool EnableSsl, string? Username, string? Password, string FromEmail, string FromName);

    private sealed record ListUsersResponse(List<UserDto> Users, int TotalCount, int Page, int PageSize);
    private sealed record UserDto(string Id, string Email, string DisplayName, bool EmailConfirmed, List<string> Roles, double Rating);
    private sealed record UpdateUserRolesRequest(List<string> Roles);
    private sealed record UpdateUserRolesResponse(bool Success, string Message);

    private async Task LoadBlockedLinksAsync()
    {
        IsLoadingBlockedLinks = true;
        try
        {
            var response = await ApiHttp.GetAsync("/api/admin/blocked-links");
            if (!response.IsSuccessStatusCode)
            {
                SetFeedback("Failed to load blocked link patterns.", true);
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
            SetFeedback("Enter a URL substring to block.", true);
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
                SetFeedback(err?.Message ?? "Failed to add pattern.", true);
                return;
            }

            var body = await response.Content.ReadFromJsonAsync<AddBlockedLinkApiResponse>();
            SetFeedback(body?.Message ?? "Pattern added.", false);
            NewBlockedPattern = string.Empty;
            NewBlockedNote = string.Empty;
            await LoadBlockedLinksAsync();
        }
        finally
        {
            IsSavingLinks = false;
        }
    }

    private async Task DeleteBlockedLinkAsync(Guid id)
    {
        IsSavingLinks = true;
        try
        {
            var response = await ApiHttp.DeleteAsync($"/api/admin/blocked-links/{id}");
            if (!response.IsSuccessStatusCode)
            {
                SetFeedback("Failed to remove pattern.", true);
                return;
            }

            SetFeedback("Pattern removed.", false);
            await LoadBlockedLinksAsync();
        }
        finally
        {
            IsSavingLinks = false;
        }
    }

    private sealed record BlockedLinkRowDto(Guid Id, string Pattern, string? Note, DateTimeOffset CreatedAtUtc);
    private sealed record AddBlockedLinkApiRequest(string Pattern, string? Note);
    private sealed record AddBlockedLinkApiResponse(bool Success, string Message, BlockedLinkRowDto? Item);

    private sealed record RegistryActivityRowDto(
        DateTimeOffset TimestampUtc,
        string Severity,
        string Action,
        string Message,
        string? TraceId,
        string? UserId,
        string? PackageName,
        string? Version);
}

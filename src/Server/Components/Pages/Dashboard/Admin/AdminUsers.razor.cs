using System.Net.Http.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.FluentUI.AspNetCore.Components;
using Icons = Microsoft.FluentUI.AspNetCore.Components.Icons;
using Server.Components.Shared;
using Server.Features.Admin;

namespace Server.Components.Pages.Dashboard.Admin;

public partial class AdminUsers
{
    private List<UserDto> Users = [];
    private IQueryable<UserDto> UsersQueryable => Users.AsQueryable();
    private bool IsLoading = true;
    private string? FeedbackMessage;
    private MessageIntent? FeedbackIntent;
    private string SearchQuery = string.Empty;
    private int CurrentPage = 1;
    private int PageSize = 50;
    private int TotalCount;
    private int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    private UserDto? SelectedUser;
    private string? SelectedUserId;

    [Inject]
    public HttpClient ApiHttp { get; set; } = default!;

    [Inject]
    public IDialogService DialogService { get; set; } = default!;

    protected override async Task OnInitializedAsync() => await LoadUsersAsync();

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
                SetFeedback("Failed to load users.", MessageIntent.Error);
                return;
            }

            var result = await response.Content.ReadFromJsonAsync<ListUsersResponse>();
            if (result is not null)
            {
                Users = result.Users;
                TotalCount = result.TotalCount;
            }

            if (SelectedUserId is not null)
            {
                SelectedUser = Users.FirstOrDefault(u => u.Id == SelectedUserId);
                if (SelectedUser is null)
                {
                    SelectedUserId = null;
                }
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

    private void SelectUserForManagement(UserDto user)
    {
        SelectedUserId = user.Id;
        SelectedUser = user;
    }

    private Task ClearSelectionAsync()
    {
        SelectedUserId = null;
        SelectedUser = null;
        return Task.CompletedTask;
    }

    private async Task OnManagementSavedAsync()
    {
        SetFeedback("User updated successfully.", MessageIntent.Success);
        await LoadUsersAsync();
    }

    private IReadOnlyList<GridActionDefinition> GetUserRowActions(UserDto user) =>
    [
        new GridActionDefinition
        {
            Icon = new Icons.Regular.Size20.PersonEdit(),
            Tooltip = "Manage user",
            OnClick = EventCallback.Factory.Create(this, () => SelectUserForManagement(user))
        }
    ];

    private async Task OpenCreateUserDialogAsync()
    {
        var content = new AdminUserCreateDialog.CreateUserDialogContent();
        var parameters = new DialogParameters
        {
            Width = "min(480px, calc(100vw - 32px))",
            Modal = true,
            TrapFocus = true,
            PreventDismissOnOverlayClick = true
        };

        var dialog = await DialogService.ShowDialogAsync<AdminUserCreateDialog>(content, parameters);
        var result = await dialog.Result;
        if (result?.Cancelled != false)
        {
            return;
        }

        SetFeedback("User created successfully.", MessageIntent.Success);
        CurrentPage = 1;
        await LoadUsersAsync();
    }

    private void SetFeedback(string message, MessageIntent intent)
    {
        FeedbackMessage = message;
        FeedbackIntent = intent;
    }
}

using System.Net.Http.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.FluentUI.AspNetCore.Components;

namespace Server.Components.Pages.Dashboard.Admin;

public partial class AdminUsers
{
    private List<UserDto> Users = [];
    private IQueryable<UserDto> UsersQueryable => Users.AsQueryable();
    private bool IsLoading = true;
    private bool IsSaving;
    private string? FeedbackMessage;
    private MessageIntent? FeedbackIntent;
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
        if (SelectedUser is null)
        {
            return;
        }

        IsSaving = true;
        try
        {
            var response = await ApiHttp.PutAsJsonAsync(
                $"/api/admin/users/{SelectedUser.Id}/roles",
                new UpdateUserRolesRequest(EditUserRoles));

            if (!response.IsSuccessStatusCode)
            {
                SetFeedback("Failed to update user roles.", MessageIntent.Error);
                return;
            }

            var result = await response.Content.ReadFromJsonAsync<UpdateUserRolesResponse>();
            if (result?.Success == true)
            {
                SetFeedback("User roles updated successfully.", MessageIntent.Success);
                CloseEditDialog();
                await LoadUsersAsync();
            }
            else
            {
                SetFeedback(result?.Message ?? "Failed to update user roles.", MessageIntent.Error);
            }
        }
        finally
        {
            IsSaving = false;
        }
    }

    private void SetFeedback(string message, MessageIntent intent)
    {
        FeedbackMessage = message;
        FeedbackIntent = intent;
    }

}

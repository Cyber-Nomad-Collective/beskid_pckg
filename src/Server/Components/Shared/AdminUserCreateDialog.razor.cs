using System.Net.Http.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.FluentUI.AspNetCore.Components;
using Server.Features.Admin;

namespace Server.Components.Shared;

public partial class AdminUserCreateDialog : IDialogContentComponent<AdminUserCreateDialog.CreateUserDialogContent>
{
    [Parameter]
    public CreateUserDialogContent Content { get; set; } = new();

    [CascadingParameter]
    public FluentDialog Dialog { get; set; } = default!;

    [Inject]
    public HttpClient Http { get; set; } = default!;

    private string? _error;
    private bool _submitting;

    public sealed class CreateUserDialogContent
    {
        public string Email { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public bool EmailConfirmed { get; set; }
        public bool IncludeModerator { get; set; }
        public bool IncludeSuperAdmin { get; set; }
    }

    private Task CancelAsync() => Dialog.CancelAsync();

    private async Task SubmitAsync()
    {
        _error = null;
        if (string.IsNullOrWhiteSpace(Content.Email)
            || string.IsNullOrWhiteSpace(Content.DisplayName)
            || string.IsNullOrWhiteSpace(Content.Password))
        {
            _error = "Email, display name, and password are required.";
            return;
        }

        var roles = new List<string> { "User" };
        if (Content.IncludeModerator)
        {
            roles.Add("Moderator");
        }

        if (Content.IncludeSuperAdmin)
        {
            roles.Add("SuperAdmin");
        }

        _submitting = true;
        try
        {
            var request = new CreateAdminUserRequest(
                Content.Email.Trim(),
                Content.Password,
                Content.DisplayName.Trim(),
                roles,
                Content.EmailConfirmed);

            var response = await Http.PostAsJsonAsync("/api/admin/users", request);
            var payload = await response.Content.ReadFromJsonAsync<CreateAdminUserResponse>();
            if (!response.IsSuccessStatusCode || payload is null || !payload.Success)
            {
                _error = payload?.Message ?? "Failed to create user.";
                return;
            }

            await Dialog.CloseAsync(true);
        }
        finally
        {
            _submitting = false;
        }
    }
}

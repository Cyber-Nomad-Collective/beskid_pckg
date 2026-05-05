using System.Net.Http.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.FluentUI.AspNetCore.Components;
using Server.Features.Admin;

namespace Server.Components.Shared;

public partial class AdminUserManagementDialog
{
    private static readonly string[] AvailableRoles = ["User", "SuperAdmin", "Moderator"];

    [Parameter, EditorRequired]
    public UserDto User { get; set; } = default!;

    [Parameter]
    public EventCallback OnSaved { get; set; }

    [Parameter]
    public EventCallback OnDismiss { get; set; }

    [Inject]
    public HttpClient Http { get; set; } = default!;

    private readonly List<string> EditRoles = [];
    private bool PublisherVerified;
    private string? PanelError;
    private bool _saving;
    private string? _syncedSourceKey;

    protected override void OnParametersSet()
    {
        var sourceKey = $"{User.Id}\u001f{User.IsPublisherVerified}\u001f{string.Join(',', User.Roles.Order(StringComparer.Ordinal))}";
        if (string.Equals(_syncedSourceKey, sourceKey, StringComparison.Ordinal))
        {
            return;
        }

        _syncedSourceKey = sourceKey;
        EditRoles.Clear();
        EditRoles.AddRange(User.Roles);
        if (!EditRoles.Contains("User"))
        {
            EditRoles.Add("User");
        }

        PublisherVerified = User.IsPublisherVerified;
        PanelError = null;
    }

    private EventCallback<bool> BuildRoleToggle(string role) =>
        EventCallback.Factory.Create<bool>(this, v => ToggleRole(role, v));

    private void ToggleRole(string role, bool isChecked)
    {
        if (role == "User")
        {
            return;
        }

        if (isChecked && !EditRoles.Contains(role))
        {
            EditRoles.Add(role);
        }
        else if (!isChecked && EditRoles.Contains(role))
        {
            EditRoles.Remove(role);
        }
    }

    private async Task DismissAsync()
    {
        if (OnDismiss.HasDelegate)
        {
            await OnDismiss.InvokeAsync();
        }
    }

    private async Task SaveAsync()
    {
        PanelError = null;
        if (!EditRoles.Contains("User"))
        {
            EditRoles.Add("User");
        }

        _saving = true;
        try
        {
            var rolesResponse = await Http.PutAsJsonAsync(
                $"/api/admin/users/{User.Id}/roles",
                new UpdateUserRolesRequest(new List<string>(EditRoles)));

            if (!rolesResponse.IsSuccessStatusCode)
            {
                PanelError = "Failed to update roles.";
                return;
            }

            var rolesPayload = await rolesResponse.Content.ReadFromJsonAsync<UpdateUserRolesResponse>();
            if (rolesPayload is not { Success: true })
            {
                PanelError = rolesPayload?.Message ?? "Failed to update roles.";
                return;
            }

            var verifiedResponse = await Http.PutAsJsonAsync(
                $"/api/admin/users/{User.Id}/publisher-verified",
                new UpdatePublisherVerifiedRequest(PublisherVerified));

            if (!verifiedResponse.IsSuccessStatusCode)
            {
                PanelError = "Roles saved, but publisher verification could not be updated.";
                return;
            }

            var verifiedPayload = await verifiedResponse.Content.ReadFromJsonAsync<UpdatePublisherVerifiedResponse>();
            if (verifiedPayload is not { Success: true })
            {
                PanelError = verifiedPayload?.Message ?? "Roles saved; verification update failed.";
                return;
            }

            if (OnSaved.HasDelegate)
            {
                await OnSaved.InvokeAsync();
            }
        }
        finally
        {
            _saving = false;
        }
    }
}

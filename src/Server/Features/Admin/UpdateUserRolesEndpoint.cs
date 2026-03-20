using FastEndpoints;
using Microsoft.AspNetCore.Identity;
using Server.Data;

namespace Server.Features.Admin;

public sealed class UpdateUserRolesEndpoint : Endpoint<UpdateUserRolesRequest, UpdateUserRolesResponse>
{
    public UserManager<ApplicationUser> UserManager { get; set; } = default!;
    public RoleManager<IdentityRole> RoleManager { get; set; } = default!;

    public override void Configure()
    {
        Put("/admin/users/{userId}/roles");
        Roles("SuperAdmin");
    }

    public override async Task HandleAsync(UpdateUserRolesRequest req, CancellationToken ct)
    {
        var userId = Route<string>("userId");
        if (string.IsNullOrWhiteSpace(userId))
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        var user = await UserManager.FindByIdAsync(userId);

        if (user is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        var currentRoles = await UserManager.GetRolesAsync(user);
        var rolesToAdd = req.Roles.Except(currentRoles).ToList();
        var rolesToRemove = currentRoles.Except(req.Roles).ToList();

        foreach (var role in rolesToAdd)
        {
            if (!await RoleManager.RoleExistsAsync(role))
            {
                await RoleManager.CreateAsync(new IdentityRole(role));
            }
            await UserManager.AddToRoleAsync(user, role);
        }

        foreach (var role in rolesToRemove)
        {
            await UserManager.RemoveFromRoleAsync(user, role);
        }

        await Send.OkAsync(new UpdateUserRolesResponse(true, "User roles updated successfully."), ct);
    }
}

public sealed record UpdateUserRolesRequest(List<string> Roles);
public sealed record UpdateUserRolesResponse(bool Success, string Message);

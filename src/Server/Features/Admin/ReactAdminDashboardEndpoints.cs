using System.Security.Claims;
using FastEndpoints;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Server.Data;
using Server.Services;

namespace Server.Features.Admin;

public sealed class UpdateReactAdminUserEndpoint(
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole> roleManager)
    : Endpoint<UpdateReactAdminUserRequest, ReactAdminUser>
{
    public override void Configure()
    {
        Patch("/admin/users/{subject}");
        Roles("SuperAdmin");
    }

    public override async Task HandleAsync(UpdateReactAdminUserRequest req, CancellationToken ct)
    {
        var subject = Route<string>("subject");
        var user = string.IsNullOrWhiteSpace(subject) ? null : await userManager.FindByIdAsync(subject);
        if (user is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        var requestedRoles = (req.Roles ?? [])
            .Where(role => !string.IsNullOrWhiteSpace(role))
            .Select(role => role.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var currentRoles = await userManager.GetRolesAsync(user);
        foreach (var role in requestedRoles.Except(currentRoles, StringComparer.Ordinal))
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                var roleResult = await roleManager.CreateAsync(new IdentityRole(role));
                if (!roleResult.Succeeded)
                {
                    AddError(string.Join(" ", roleResult.Errors.Select(error => error.Description)));
                    await Send.ErrorsAsync(StatusCodes.Status400BadRequest, ct);
                    return;
                }
            }
            await userManager.AddToRoleAsync(user, role);
        }
        foreach (var role in currentRoles.Except(requestedRoles, StringComparer.Ordinal))
        {
            await userManager.RemoveFromRoleAsync(user, role);
        }

        user.IsPublisherVerified = req.PublisherVerified;
        var updateResult = await userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            AddError(string.Join(" ", updateResult.Errors.Select(error => error.Description)));
            await Send.ErrorsAsync(StatusCodes.Status400BadRequest, ct);
            return;
        }

        await Send.OkAsync(await ReactAdminUser.FromUserAsync(userManager, user), ct);
    }
}

public sealed class ListReactAdminPermissionsEndpoint(ApplicationDbContext db)
    : EndpointWithoutRequest<List<ReactAdminPermission>>
{
    public override void Configure()
    {
        Get("/admin/permissions");
        Roles("SuperAdmin");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var permissions = await db.ResourcePermissions.AsNoTracking()
            .OrderBy(permission => permission.ResourceType)
            .ThenBy(permission => permission.ResourceId)
            .ThenBy(permission => permission.UserId)
            .Select(permission => new ReactAdminPermission(
                permission.UserId,
                permission.ResourceType.ToLowerInvariant() + ":" + permission.ResourceId,
                permission.Permission))
            .ToListAsync(ct);
        await Send.OkAsync(permissions, ct);
    }
}

public sealed record UpdateReactAdminUserRequest(IReadOnlyList<string>? Roles, bool PublisherVerified);
public sealed record ReactAdminPermission(string Subject, string Resource, string Capability)
{
    public static (string Type, string Id)? ParseResource(string? value)
    {
        var separator = value?.IndexOf(':') ?? -1;
        if (separator <= 0 || separator == value!.Length - 1)
        {
            return null;
        }
        return (value[..separator].Trim(), value[(separator + 1)..].Trim());
    }
}

public sealed record ReactAdminUser(string Subject, string GithubLogin, IReadOnlyList<string> Roles, bool PublisherVerified)
{
    public static async Task<ReactAdminUser> FromUserAsync(UserManager<ApplicationUser> userManager, ApplicationUser user)
        => new(user.Id, user.UserName ?? user.Email ?? user.Id, (await userManager.GetRolesAsync(user)).ToList(), user.IsPublisherVerified);
}

using FastEndpoints;
using Microsoft.AspNetCore.Identity;
using Server.Data;

namespace Server.Features.Admin;

public sealed class CreateAdminUserEndpoint(
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole> roleManager)
    : Endpoint<CreateAdminUserRequest, CreateAdminUserResponse>
{
    private static readonly HashSet<string> AllowedRoles = new(StringComparer.Ordinal)
    {
        "User",
        "SuperAdmin",
        "Moderator",
    };

    public override void Configure()
    {
        Post("/admin/users");
        Roles("SuperAdmin");
    }

    public override async Task HandleAsync(CreateAdminUserRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Email)
            || string.IsNullOrWhiteSpace(req.Password)
            || string.IsNullOrWhiteSpace(req.DisplayName))
        {
            await Send.ResponseAsync(new CreateAdminUserResponse(false, "Email, password, and display name are required.", null), StatusCodes.Status400BadRequest, ct);
            return;
        }

        var email = req.Email.Trim();
        var normalizedEmail = userManager.NormalizeEmail(email);
        if (await userManager.FindByEmailAsync(email) is not null)
        {
            await Send.ResponseAsync(new CreateAdminUserResponse(false, "A user with this email already exists.", null), StatusCodes.Status409Conflict, ct);
            return;
        }

        var roleSet = new HashSet<string>(StringComparer.Ordinal);
        if (req.Roles is { Count: > 0 })
        {
            foreach (var r in req.Roles)
            {
                if (string.IsNullOrWhiteSpace(r))
                {
                    continue;
                }

                var trimmed = r.Trim();
                if (!AllowedRoles.Contains(trimmed))
                {
                    await Send.ResponseAsync(new CreateAdminUserResponse(false, $"Role '{trimmed}' is not allowed.", null), StatusCodes.Status400BadRequest, ct);
                    return;
                }

                roleSet.Add(trimmed);
            }
        }

        roleSet.Add("User");

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid().ToString("N"),
            UserName = email,
            NormalizedUserName = userManager.NormalizeName(email),
            Email = email,
            NormalizedEmail = normalizedEmail,
            EmailConfirmed = req.EmailConfirmed,
            DisplayName = req.DisplayName.Trim(),
        };

        var createResult = await userManager.CreateAsync(user, req.Password);
        if (!createResult.Succeeded)
        {
            var message = string.Join(' ', createResult.Errors.Select(e => e.Description));
            await Send.ResponseAsync(new CreateAdminUserResponse(false, message, null), StatusCodes.Status400BadRequest, ct);
            return;
        }

        foreach (var role in roleSet)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }

            await userManager.AddToRoleAsync(user, role);
        }

        await Send.OkAsync(new CreateAdminUserResponse(true, "User created.", user.Id), ct);
    }
}

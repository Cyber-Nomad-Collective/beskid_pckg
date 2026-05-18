using FastEndpoints;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Server.Data;

namespace Server.Features.Users;

public sealed class CreateInitialAdminEndpoint(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    RoleManager<IdentityRole> roleManager)
    : Endpoint<CreateInitialAdminRequest, AuthActionResponse>
{
    public override void Configure()
    {
        Post("/users/bootstrap-admin");
        AllowAnonymous();
        Summary(s => s.Summary = "Create initial SuperAdmin account when no users exist.");
    }

    public override async Task HandleAsync(CreateInitialAdminRequest req, CancellationToken ct)
    {
        if (await userManager.Users.AnyAsync(ct))
        {
            await Send.ResponseAsync(new AuthActionResponse(false, "Initial admin already exists."), StatusCodes.Status409Conflict, ct);
            return;
        }

        if (string.IsNullOrWhiteSpace(req.DisplayName)
            || string.IsNullOrWhiteSpace(req.Email)
            || string.IsNullOrWhiteSpace(req.Password)
            || string.IsNullOrWhiteSpace(req.ConfirmPassword))
        {
            await Send.ResponseAsync(new AuthActionResponse(false, "Display name, email, password, and password confirmation are required."), StatusCodes.Status400BadRequest, ct);
            return;
        }

        if (!string.Equals(req.Password, req.ConfirmPassword, StringComparison.Ordinal))
        {
            await Send.ResponseAsync(new AuthActionResponse(false, "Passwords do not match."), StatusCodes.Status400BadRequest, ct);
            return;
        }

        var email = req.Email.Trim();
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid().ToString("N"),
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = req.DisplayName.Trim(),
        };

        var createResult = await userManager.CreateAsync(user, req.Password);
        if (!createResult.Succeeded)
        {
            var message = string.Join(' ', createResult.Errors.Select(e => e.Description));
            await Send.ResponseAsync(new AuthActionResponse(false, message), StatusCodes.Status400BadRequest, ct);
            return;
        }

        if (!await roleManager.RoleExistsAsync("SuperAdmin"))
        {
            await roleManager.CreateAsync(new IdentityRole("SuperAdmin"));
        }

        await userManager.AddToRoleAsync(user, "SuperAdmin");

        var signInResult = await signInManager.PasswordSignInAsync(email, req.Password, isPersistent: true, lockoutOnFailure: false);
        if (!signInResult.Succeeded)
        {
            await Send.ResponseAsync(new AuthActionResponse(false, "Admin account created but automatic sign-in failed."), StatusCodes.Status500InternalServerError, ct);
            return;
        }

        await Send.OkAsync(new AuthActionResponse(true, "Initial SuperAdmin account created."), ct);
    }
}

public sealed record CreateInitialAdminRequest(string DisplayName, string Email, string Password, string ConfirmPassword);

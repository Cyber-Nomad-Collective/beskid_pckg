using FastEndpoints;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using pckg.Data;

namespace pckg.Features.Users;

public sealed class RegisterUserEndpoint(UserManager<ApplicationUser> userManager)
    : Endpoint<RegisterUserRequest, AuthActionResponse>
{
    public override void Configure()
    {
        Post("/users/register");
        AllowAnonymous();
        Summary(s => s.Summary = "Register a new user account.");
    }

    public override async Task HandleAsync(RegisterUserRequest req, CancellationToken ct)
    {
        if (!await userManager.Users.AnyAsync(ct))
        {
            HttpContext.Response.StatusCode = StatusCodes.Status409Conflict;
            await Send.OkAsync(new AuthActionResponse(false, "No users exist yet. Complete onboarding first."), ct);
            return;
        }

        if (string.IsNullOrWhiteSpace(req.Email)
            || string.IsNullOrWhiteSpace(req.Password)
            || string.IsNullOrWhiteSpace(req.ConfirmPassword))
        {
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await Send.OkAsync(new AuthActionResponse(false, "Email, password, and password confirmation are required."), ct);
            return;
        }

        if (!string.Equals(req.Password, req.ConfirmPassword, StringComparison.Ordinal))
        {
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await Send.OkAsync(new AuthActionResponse(false, "Passwords do not match."), ct);
            return;
        }

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid().ToString("N"),
            UserName = req.Email.Trim(),
            Email = req.Email.Trim(),
            EmailConfirmed = true,
        };

        var result = await userManager.CreateAsync(user, req.Password);
        if (!result.Succeeded)
        {
            var message = string.Join(' ', result.Errors.Select(e => e.Description));
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await Send.OkAsync(new AuthActionResponse(false, message), ct);
            return;
        }

        await Send.OkAsync(new AuthActionResponse(true, "Registration successful. You can now sign in."), ct);
    }
}

public sealed record RegisterUserRequest(string Email, string Password, string ConfirmPassword);
public sealed record AuthActionResponse(bool Success, string Message);

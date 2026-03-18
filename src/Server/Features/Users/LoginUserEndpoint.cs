using FastEndpoints;
using Microsoft.AspNetCore.Identity;
using pckg.Data;

namespace pckg.Features.Users;

public sealed class LoginUserEndpoint(SignInManager<ApplicationUser> signInManager)
    : Endpoint<LoginUserRequest, AuthActionResponse>
{
    public override void Configure()
    {
        Post("/users/login");
        AllowAnonymous();
        Summary(s => s.Summary = "Sign in using cookie authentication.");
    }

    public override async Task HandleAsync(LoginUserRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Password))
        {
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await Send.OkAsync(new AuthActionResponse(false, "Email and password are required."), ct);
            return;
        }

        var result = await signInManager.PasswordSignInAsync(req.Email.Trim(), req.Password, req.RememberMe, lockoutOnFailure: false);
        if (!result.Succeeded)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await Send.OkAsync(new AuthActionResponse(false, "Invalid credentials."), ct);
            return;
        }

        await Send.OkAsync(new AuthActionResponse(true, "Login successful."), ct);
    }
}

public sealed record LoginUserRequest(string Email, string Password, bool RememberMe);

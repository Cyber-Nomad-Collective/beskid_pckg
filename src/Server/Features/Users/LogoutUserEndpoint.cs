using FastEndpoints;
using Microsoft.AspNetCore.Identity;
using pckg.Data;

namespace pckg.Features.Users;

public sealed class LogoutUserEndpoint(SignInManager<ApplicationUser> signInManager)
    : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("/users/logout");
        AllowAnonymous();
        Summary(s => s.Summary = "Sign out current user and redirect.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        await signInManager.SignOutAsync();
        var returnUrl = Query<string>("returnUrl", false) ?? "/";
        HttpContext.Response.Redirect(returnUrl);
    }
}

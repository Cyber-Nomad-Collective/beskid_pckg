using FastEndpoints;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Server.Data;

namespace Server.Features.Users;

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
        await HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
        await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);
        await HttpContext.SignOutAsync(IdentityConstants.TwoFactorUserIdScheme);

        var requestedReturnUrl = Query<string>("returnUrl", false);
        var returnUrl = IsLocalReturnUrl(requestedReturnUrl) ? requestedReturnUrl! : "/";

        await Send.RedirectAsync(returnUrl, isPermanent: false);
    }

    private static bool IsLocalReturnUrl(string? returnUrl)
    {
        return !string.IsNullOrWhiteSpace(returnUrl)
               && returnUrl.StartsWith("/", StringComparison.Ordinal)
               && !returnUrl.StartsWith("//", StringComparison.Ordinal);
    }
}

using System.Security.Claims;
using FastEndpoints;
using Microsoft.AspNetCore.Identity;
using Server.Data;

namespace Server.Features.Auth;

/// <summary>React-facing session projection over the registry's sole Identity session.</summary>
public sealed class ReactSessionEndpoint(UserManager<ApplicationUser> userManager)
    : EndpointWithoutRequest<ReactSessionResponse>
{
    public override void Configure()
    {
        Get("/auth/session");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var subject = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(subject))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }

        var user = await userManager.FindByIdAsync(subject);
        if (user is null)
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }

        await Send.OkAsync(new ReactSessionResponse(
            user.Id,
            user.UserName ?? user.Email ?? user.Id,
            user.SecurityStamp ?? user.Id), ct);
    }
}

public sealed record ReactSessionResponse(string Subject, string GithubLogin, string HubSessionId);

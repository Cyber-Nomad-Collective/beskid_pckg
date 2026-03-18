using FastEndpoints;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using pckg.Data;

namespace pckg.Features.Users;

public sealed class GetBootstrapStatusEndpoint(UserManager<ApplicationUser> userManager)
    : EndpointWithoutRequest<BootstrapStatusResponse>
{
    public override void Configure()
    {
        Get("/users/bootstrap-status");
        AllowAnonymous();
        Summary(s => s.Summary = "Get whether initial admin onboarding is required.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var hasUsers = await userManager.Users.AnyAsync(ct);
        await Send.OkAsync(new BootstrapStatusResponse(hasUsers), ct);
    }
}

public sealed record BootstrapStatusResponse(bool HasUsers);

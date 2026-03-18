using FastEndpoints;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;
using pckg.Data;

namespace pckg.Features.Users;

public sealed class GetCurrentUserEndpoint(UserManager<ApplicationUser> userManager)
    : EndpointWithoutRequest<CurrentUserResponse>
{
    public override void Configure()
    {
        Get("/users/me");
        AllowAnonymous();
        Summary(s => s.Summary = "Get current authentication/profile state.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            await Send.OkAsync(new CurrentUserResponse(false, null, null, false, null, null, null, null, null, null), ct);
            return;
        }

        var user = await userManager.FindByIdAsync(userId);
        await Send.OkAsync(
            new CurrentUserResponse(
                true,
                userId,
                user?.Email,
                true,
                string.IsNullOrWhiteSpace(user?.DisplayName) ? user?.Email : user.DisplayName,
                user?.Bio,
                user?.GitHubUrl,
                user?.WebsiteUrl,
                user?.XUrl,
                user?.ProfileImageUrl),
            ct);
    }
}

public sealed record CurrentUserResponse(
    bool IsAuthenticated,
    string? UserId,
    string? Email,
    bool IsPublisher,
    string? DisplayName,
    string? Bio,
    string? GitHubUrl,
    string? WebsiteUrl,
    string? XUrl,
    string? ProfileImageUrl);

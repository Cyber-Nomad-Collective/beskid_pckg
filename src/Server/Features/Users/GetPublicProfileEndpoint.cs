using FastEndpoints;
using Microsoft.AspNetCore.Identity;
using pckg.Data;

namespace pckg.Features.Users;

public sealed class GetPublicProfileEndpoint(UserManager<ApplicationUser> userManager)
    : EndpointWithoutRequest<PublicProfileResponse>
{
    public override void Configure()
    {
        Get("/users/public/{userId}");
        AllowAnonymous();
        Summary(s => s.Summary = "Get public profile for a given user id.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var userId = Route<string>("userId");
        if (string.IsNullOrWhiteSpace(userId))
        {
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await Send.OkAsync(new PublicProfileResponse(false, "User id is required.", null), ct);
            return;
        }

        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status404NotFound;
            await Send.OkAsync(new PublicProfileResponse(false, "Profile not found.", null), ct);
            return;
        }

        var displayName = string.IsNullOrWhiteSpace(user.DisplayName)
            ? user.UserName ?? "User"
            : user.DisplayName;

        await Send.OkAsync(
            new PublicProfileResponse(
                true,
                "Profile loaded.",
                new PublicProfilePayload(
                    user.Id,
                    displayName,
                    string.IsNullOrWhiteSpace(user.Bio) ? null : user.Bio,
                    string.IsNullOrWhiteSpace(user.GitHubUrl) ? null : user.GitHubUrl,
                    string.IsNullOrWhiteSpace(user.WebsiteUrl) ? null : user.WebsiteUrl,
                    string.IsNullOrWhiteSpace(user.XUrl) ? null : user.XUrl,
                    string.IsNullOrWhiteSpace(user.ProfileImageUrl) ? null : user.ProfileImageUrl)),
            ct);
    }
}

public sealed record PublicProfileResponse(bool Success, string Message, PublicProfilePayload? Profile);

public sealed record PublicProfilePayload(
    string UserId,
    string DisplayName,
    string? Bio,
    string? GitHubUrl,
    string? WebsiteUrl,
    string? XUrl,
    string? ProfileImageUrl);

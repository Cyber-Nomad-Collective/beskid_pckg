using FastEndpoints;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Server.Data;

namespace Server.Features.Users;

public sealed class GetCurrentUserEndpoint(
    UserManager<ApplicationUser> userManager,
    ApplicationDbContext dbContext)
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
            await Send.OkAsync(new CurrentUserResponse(false, null, null, false, null, null, null, null, null, [], null), ct);
            return;
        }

        var user = await userManager.FindByIdAsync(userId);
        var email = user?.Email;
        if (string.IsNullOrWhiteSpace(email))
        {
            email = await dbContext.UserEmails
                .AsNoTracking()
                .Where(x => x.UserId == userId)
                .OrderByDescending(x => x.IsPrimary)
                .ThenBy(x => x.AddedAtUtc)
                .Select(x => x.Email)
                .FirstOrDefaultAsync(ct);
        }

        var socialLinks = ProfileSocialLinks.FromUser(user);

        await Send.OkAsync(
            new CurrentUserResponse(
                true,
                userId,
                email,
                true,
                string.IsNullOrWhiteSpace(user?.DisplayName) ? email : user.DisplayName,
                user?.Bio,
                ProfileSocialLinks.GetLegacyUrl(socialLinks, Components.Shared.SocialPlatform.GitHub),
                ProfileSocialLinks.GetLegacyUrl(socialLinks, Components.Shared.SocialPlatform.Website),
                ProfileSocialLinks.GetLegacyUrl(socialLinks, Components.Shared.SocialPlatform.X),
                socialLinks,
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
    IReadOnlyList<ProfileSocialLink> SocialLinks,
    string? ProfileImageUrl);

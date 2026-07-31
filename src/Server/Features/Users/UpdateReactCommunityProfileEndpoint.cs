using System.Security.Claims;
using FastEndpoints;
using Microsoft.AspNetCore.Identity;
using Server.Components.Shared;
using Server.Data;

namespace Server.Features.Users;

/// <summary>Updates the same profile fields exposed by the public React profile projection.</summary>
public sealed class UpdateReactCommunityProfileEndpoint(UserManager<ApplicationUser> userManager)
    : Endpoint<UpdateReactCommunityProfileRequest, ReactCommunityProfile>
{
    public override void Configure()
    {
        Put("/community/profiles/me");
        Options(options => options.RequireAuthorization());
    }

    public override async Task HandleAsync(UpdateReactCommunityProfileRequest req, CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }

        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        var links = ProfileSocialLinks.Normalize((req.SocialLinks ?? []).Select(url => new ProfileSocialLink(SocialPlatformCatalog.DetectPlatform(url), url)));
        user.DisplayName = req.DisplayName?.Trim() ?? string.Empty;
        user.Bio = req.Bio?.Trim() ?? string.Empty;
        user.SocialLinksJson = ProfileSocialLinks.Serialize(links);
        user.GitHubUrl = ProfileSocialLinks.GetLegacyUrl(links, SocialPlatform.GitHub);
        user.WebsiteUrl = ProfileSocialLinks.GetLegacyUrl(links, SocialPlatform.Website);
        user.XUrl = ProfileSocialLinks.GetLegacyUrl(links, SocialPlatform.X);

        var update = await userManager.UpdateAsync(user);
        if (!update.Succeeded)
        {
            foreach (var error in update.Errors)
            {
                AddError(error.Description);
            }
            await Send.ErrorsAsync(StatusCodes.Status400BadRequest, ct);
            return;
        }

        await Send.OkAsync(ReactCommunityProfile.FromUser(user), ct);
    }
}

public sealed class UpdateReactCommunityProfileRequest
{
    public string? DisplayName { get; init; }
    public string? Bio { get; init; }
    public IReadOnlyList<string>? SocialLinks { get; init; }
}

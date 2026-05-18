using FastEndpoints;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;
using Server.Components.Shared;
using Server.Data;

namespace Server.Features.Users;

public sealed class UpdateProfileEndpoint(UserManager<ApplicationUser> userManager)
    : Endpoint<UpdateProfileRequest, UpdateProfileResponse>
{
    public override void Configure()
    {
        Put("/users/profile");
        Options(x => x.RequireAuthorization());
        Summary(s => s.Summary = "Update current user's profile settings.");
    }

    public override async Task HandleAsync(UpdateProfileRequest req, CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            await Send.ResponseAsync(new UpdateProfileResponse(false, "Unauthorized.", null), StatusCodes.Status401Unauthorized, ct);
            return;
        }

        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
        {
            await Send.ResponseAsync(new UpdateProfileResponse(false, "User not found.", null), StatusCodes.Status404NotFound, ct);
            return;
        }

        var socialLinks = req.SocialLinks is null
            ? ProfileSocialLinks.FromLegacy(req.GitHubUrl, req.WebsiteUrl, req.XUrl)
            : ProfileSocialLinks.Normalize(req.SocialLinks);

        user.DisplayName = Normalize(req.DisplayName);
        user.Bio = Normalize(req.Bio);
        user.GitHubUrl = ProfileSocialLinks.GetLegacyUrl(socialLinks, SocialPlatform.GitHub);
        user.WebsiteUrl = ProfileSocialLinks.GetLegacyUrl(socialLinks, SocialPlatform.Website);
        user.XUrl = ProfileSocialLinks.GetLegacyUrl(socialLinks, SocialPlatform.X);
        user.SocialLinksJson = ProfileSocialLinks.Serialize(socialLinks);

        var result = await userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            await Send.ResponseAsync(new UpdateProfileResponse(false, string.Join(" ", result.Errors.Select(x => x.Description)), null), StatusCodes.Status400BadRequest, ct);
            return;
        }

        await Send.OkAsync(new UpdateProfileResponse(true, "Profile updated.", new ProfilePayload(
            user.Email,
            user.DisplayName,
            user.Bio,
            user.GitHubUrl,
            user.WebsiteUrl,
            user.XUrl,
            socialLinks,
            user.ProfileImageUrl)), ct);
    }

    private static string Normalize(string? value) => value?.Trim() ?? string.Empty;
}

public sealed class UpdateProfileRequest
{
    public string? DisplayName { get; init; }
    public string? Bio { get; init; }
    public string? GitHubUrl { get; init; }
    public string? WebsiteUrl { get; init; }
    public string? XUrl { get; init; }
    public IReadOnlyList<ProfileSocialLink>? SocialLinks { get; init; }
}

public sealed record UpdateProfileResponse(bool Success, string Message, ProfilePayload? Profile);
public sealed record ProfilePayload(
    string? Email,
    string? DisplayName,
    string? Bio,
    string? GitHubUrl,
    string? WebsiteUrl,
    string? XUrl,
    IReadOnlyList<ProfileSocialLink> SocialLinks,
    string? ProfileImageUrl);

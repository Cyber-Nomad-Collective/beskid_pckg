using FastEndpoints;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;
using pckg.Data;

namespace pckg.Features.Users;

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
            HttpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await Send.OkAsync(new UpdateProfileResponse(false, "Unauthorized.", null), ct);
            return;
        }

        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status404NotFound;
            await Send.OkAsync(new UpdateProfileResponse(false, "User not found.", null), ct);
            return;
        }

        user.DisplayName = Normalize(req.DisplayName);
        user.Bio = Normalize(req.Bio);
        user.GitHubUrl = Normalize(req.GitHubUrl);
        user.WebsiteUrl = Normalize(req.WebsiteUrl);
        user.XUrl = Normalize(req.XUrl);

        var result = await userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await Send.OkAsync(new UpdateProfileResponse(false, string.Join(" ", result.Errors.Select(x => x.Description)), null), ct);
            return;
        }

        await Send.OkAsync(new UpdateProfileResponse(true, "Profile updated.", new ProfilePayload(
            user.Email,
            user.DisplayName,
            user.Bio,
            user.GitHubUrl,
            user.WebsiteUrl,
            user.XUrl,
            user.ProfileImageUrl)), ct);
    }

    private static string Normalize(string? value) => value?.Trim() ?? string.Empty;
}

public sealed record UpdateProfileRequest(string? DisplayName, string? Bio, string? GitHubUrl, string? WebsiteUrl, string? XUrl);
public sealed record UpdateProfileResponse(bool Success, string Message, ProfilePayload? Profile);
public sealed record ProfilePayload(string? Email, string? DisplayName, string? Bio, string? GitHubUrl, string? WebsiteUrl, string? XUrl, string? ProfileImageUrl);

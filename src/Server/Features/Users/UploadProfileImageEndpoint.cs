using FastEndpoints;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;
using pckg.Data;

namespace pckg.Features.Users;

public sealed class UploadProfileImageEndpoint(UserManager<ApplicationUser> userManager, IWebHostEnvironment environment)
    : EndpointWithoutRequest<UploadProfileImageResponse>
{
    private const long MaxFileSizeBytes = 5 * 1024 * 1024;
    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/png",
        "image/jpeg",
        "image/webp",
        "image/gif"
    };

    public override void Configure()
    {
        Post("/users/profile/image");
        Options(x => x.RequireAuthorization());
        Summary(s => s.Summary = "Upload current user's profile picture.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            HttpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await Send.OkAsync(new UploadProfileImageResponse(false, "Unauthorized.", null), ct);
            return;
        }

        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status404NotFound;
            await Send.OkAsync(new UploadProfileImageResponse(false, "User not found.", null), ct);
            return;
        }

        if (!HttpContext.Request.HasFormContentType)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await Send.OkAsync(new UploadProfileImageResponse(false, "Expected multipart form payload.", null), ct);
            return;
        }

        var form = await HttpContext.Request.ReadFormAsync(ct);
        var file = form.Files.GetFile("image");
        if (file is null)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await Send.OkAsync(new UploadProfileImageResponse(false, "Profile image is required.", null), ct);
            return;
        }

        if (file.Length <= 0 || file.Length > MaxFileSizeBytes)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await Send.OkAsync(new UploadProfileImageResponse(false, "Image size must be between 1 byte and 5 MB.", null), ct);
            return;
        }

        if (!AllowedContentTypes.Contains(file.ContentType))
        {
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await Send.OkAsync(new UploadProfileImageResponse(false, "Unsupported image type.", null), ct);
            return;
        }

        var extension = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(extension))
        {
            extension = file.ContentType switch
            {
                "image/png" => ".png",
                "image/jpeg" => ".jpg",
                "image/webp" => ".webp",
                "image/gif" => ".gif",
                _ => ".img"
            };
        }

        var uploadsRoot = Path.Combine(environment.WebRootPath ?? Path.Combine(environment.ContentRootPath, "wwwroot"), "uploads", "profiles");
        Directory.CreateDirectory(uploadsRoot);

        var fileName = $"{userId}{extension.ToLowerInvariant()}";
        var filePath = Path.Combine(uploadsRoot, fileName);

        await using (var stream = File.Create(filePath))
        {
            await file.CopyToAsync(stream, ct);
        }

        user.ProfileImageUrl = $"/uploads/profiles/{fileName}";
        var result = await userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await Send.OkAsync(new UploadProfileImageResponse(false, string.Join(" ", result.Errors.Select(x => x.Description)), null), ct);
            return;
        }

        await Send.OkAsync(new UploadProfileImageResponse(true, "Profile picture updated.", user.ProfileImageUrl), ct);
    }
}

public sealed record UploadProfileImageResponse(bool Success, string Message, string? ProfileImageUrl);

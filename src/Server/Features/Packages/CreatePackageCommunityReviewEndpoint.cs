using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using Server.Data;
using Server.Services;

namespace Server.Features.Packages;

public sealed class CreatePackageCommunityReviewEndpoint(
    ApplicationDbContext dbContext,
    IApiPrincipalResolver principalResolver,
    ICaptchaVerificationService captcha,
    ILinkContentGuard linkGuard,
    IHtmlSanitizationService htmlSanitization)
    : Endpoint<CreatePackageCommunityReviewRequest, CreatePackageCommunityReviewResponse>
{
    public override void Configure()
    {
        Post("/packages/{packageName}/community-reviews");
        Options(x => x.RequireAuthorization());
        Roles("User", "SuperAdmin", "Moderator");
    }

    public override async Task HandleAsync(CreatePackageCommunityReviewRequest req, CancellationToken ct)
    {
        var userId = await principalResolver.ResolveUserIdAsync(HttpContext, ct);
        if (string.IsNullOrWhiteSpace(userId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }

        var name = Route<string>("packageName")?.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await Send.OkAsync(new CreatePackageCommunityReviewResponse(false, "Package name is required."), ct);
            return;
        }

        var package = await dbContext.Packages.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Name == name && x.IsPublic, ct);
        if (package is null)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status404NotFound;
            await Send.OkAsync(new CreatePackageCommunityReviewResponse(false, "Package not found."), ct);
            return;
        }

        var remoteIp = HttpContext.Connection.RemoteIpAddress?.ToString();
        if (!await captcha.IsHumanAsync(req.CaptchaToken, remoteIp, ct))
        {
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await Send.OkAsync(new CreatePackageCommunityReviewResponse(false, "Robot check failed. Please try again."), ct);
            return;
        }

        var rawComment = req.Comment?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(rawComment))
        {
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await Send.OkAsync(new CreatePackageCommunityReviewResponse(false, "Review text is required."), ct);
            return;
        }

        var linkBlock = await linkGuard.GetBlockReasonAsync(rawComment, ct);
        if (linkBlock is not null)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await Send.OkAsync(new CreatePackageCommunityReviewResponse(false, linkBlock), ct);
            return;
        }

        var rating = Math.Clamp(req.Rating, 1, 5);
        var sanitized = htmlSanitization.Sanitize(rawComment);
        if (string.IsNullOrWhiteSpace(sanitized))
        {
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await Send.OkAsync(new CreatePackageCommunityReviewResponse(false, "Review text is empty after sanitization."), ct);
            return;
        }

        dbContext.PackageCommunityReviews.Add(new PackageCommunityReviewEntity
        {
            Id = Guid.NewGuid(),
            PackageId = package.Id,
            UserId = userId,
            Rating = rating,
            Comment = sanitized,
            CreatedAtUtc = DateTimeOffset.UtcNow,
        });
        await dbContext.SaveChangesAsync(ct);

        await Send.OkAsync(new CreatePackageCommunityReviewResponse(true, "Review posted."), ct);
    }
}

public sealed record CreatePackageCommunityReviewRequest(int Rating, string Comment, string? CaptchaToken);

public sealed record CreatePackageCommunityReviewResponse(bool Success, string Message);

using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using Server.Data;
using Server.Services;

namespace Server.Features.Packages;

public sealed class ReviewActionEndpoint(
    ApplicationDbContext dbContext,
    IApiPrincipalResolver principalResolver)
    : Endpoint<ReviewActionRequest, ReviewActionResponse>
{
    private static readonly HashSet<string> AllowedActions = new(StringComparer.OrdinalIgnoreCase)
    {
        "Approve",
        "NeedsChanges",
        "Reject",
    };

    public override void Configure()
    {
        Post("/packages/reviews/{ReviewId}/actions");
        Options(x => x.RequireAuthorization());
        Summary(s =>
        {
            s.Summary = "Apply moderation action to a package review item.";
        });
    }

    public override async Task HandleAsync(ReviewActionRequest req, CancellationToken ct)
    {
        var userId = await principalResolver.ResolveUserIdAsync(HttpContext, ct);
        if (string.IsNullOrWhiteSpace(userId))
        {
            HttpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await Send.OkAsync(new ReviewActionResponse(false, "Unauthorized.", null), ct);
            return;
        }

        if (!AllowedActions.Contains(req.Action ?? string.Empty))
        {
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await Send.OkAsync(new ReviewActionResponse(false, "Action must be one of: Approve, NeedsChanges, Reject.", null), ct);
            return;
        }

        var action = req.Action!.Trim();

        var review = await dbContext.PackageReviews
            .Include(x => x.Package)
            .SingleOrDefaultAsync(x => x.Id == req.ReviewId, ct);

        if (review is null)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status404NotFound;
            await Send.OkAsync(new ReviewActionResponse(false, "Review item not found.", null), ct);
            return;
        }

        if (review.Package is null || review.Package.OwnerUserId != userId)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status403Forbidden;
            await Send.OkAsync(new ReviewActionResponse(false, "You cannot modify this review.", null), ct);
            return;
        }

        var package = review.Package;

        review.Status = action;
        review.ReviewerUserId = userId;
        review.ReviewNotes = string.IsNullOrWhiteSpace(req.Notes) ? null : req.Notes.Trim();
        review.ReviewedAtUtc = DateTimeOffset.UtcNow;

        await dbContext.SaveChangesAsync(ct);

        var response = new PackageReviewResponse(
            review.Id,
            review.PackageId,
            package.Name,
            review.RequestedByUserId,
            review.Reason,
            review.Status,
            review.SubmittedAtUtc,
            review.ReviewerUserId,
            review.ReviewNotes,
            review.ReviewedAtUtc);

        await Send.OkAsync(new ReviewActionResponse(true, "Review updated.", response), ct);
    }
}

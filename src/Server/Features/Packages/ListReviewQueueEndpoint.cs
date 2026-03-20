using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using Server.Data;
using Server.Services;

namespace Server.Features.Packages;

public sealed class ListReviewQueueEndpoint(
    ApplicationDbContext dbContext,
    IApiPrincipalResolver principalResolver)
    : EndpointWithoutRequest<List<PackageReviewResponse>>
{
    public override void Configure()
    {
        Get("/packages/reviews");
        Options(x => x.RequireAuthorization());
        Summary(s =>
        {
            s.Summary = "List review queue for packages owned by current user.";
        });
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var userId = await principalResolver.ResolveUserIdAsync(HttpContext, ct);
        if (string.IsNullOrWhiteSpace(userId))
        {
            HttpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await Send.OkAsync([], ct);
            return;
        }

        var query = from review in dbContext.PackageReviews.AsNoTracking()
                    join package in dbContext.Packages.AsNoTracking() on review.PackageId equals package.Id
                    where package.OwnerUserId == userId
                    orderby review.SubmittedAtUtc descending
                    select new PackageReviewResponse(
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

        var response = await query.ToListAsync(ct);
        await Send.OkAsync(response, ct);
    }
}

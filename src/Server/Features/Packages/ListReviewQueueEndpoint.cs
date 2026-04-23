using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using Server.Data;
using Server.Services;

namespace Server.Features.Packages;

public sealed class ListReviewQueueEndpoint(
    ApplicationDbContext dbContext,
    IApiPrincipalResolver principalResolver,
    IAuthorizationService authorization)
    : EndpointWithoutRequest<List<PackageReviewResponse>>
{
    public override void Configure()
    {
        Get("/packages/reviews");
        Options(x => x.RequireAuthorization());
        Summary(s =>
        {
            s.Summary = "List review queue for packages you can moderate.";
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

        var seeAll = await authorization.IsSuperAdminAsync(userId)
                     || await authorization.IsGlobalModeratorAsync(userId);

        var delegatedPackageIds = await dbContext.ResourcePermissions
            .AsNoTracking()
            .Where(p => p.UserId == userId && p.ResourceType == "Package" && p.Permission == "Moderate")
            .Select(p => p.ResourceId)
            .ToListAsync(ct);

        var delegated = delegatedPackageIds
            .Select(s => Guid.TryParse(s, out var g) ? g : (Guid?)null)
            .Where(g => g.HasValue)
            .Select(g => g!.Value)
            .ToHashSet();

        var query = from review in dbContext.PackageReviews.AsNoTracking()
                    join package in dbContext.Packages.AsNoTracking() on review.PackageId equals package.Id
                    where seeAll
                          || package.OwnerUserId == userId
                          || delegated.Contains(package.Id)
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

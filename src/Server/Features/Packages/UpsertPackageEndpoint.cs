using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using Server.Data;
using Server.Services;

namespace Server.Features.Packages;

public sealed class UpsertPackageEndpoint(
    ApplicationDbContext dbContext,
    IApiPrincipalResolver principalResolver)
    : Endpoint<UpsertPackageRequest, UpsertPackageResponse>
{
    public override void Configure()
    {
        Post("/packages");
        Options(x => x.RequireAuthorization());
        Summary(s =>
        {
            s.Summary = "Create or update a package and optionally submit review.";
        });
    }

    public override async Task HandleAsync(UpsertPackageRequest req, CancellationToken ct)
    {
        var userId = await principalResolver.ResolveUserIdAsync(HttpContext, ct);
        if (string.IsNullOrWhiteSpace(userId))
        {
            HttpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await Send.OkAsync(new UpsertPackageResponse(false, "Unauthorized.", null, null), ct);
            return;
        }

        var normalizedName = req.Name?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await Send.OkAsync(new UpsertPackageResponse(false, "Package name is required.", null, null), ct);
            return;
        }

        var entity = await dbContext.Packages.SingleOrDefaultAsync(x => x.Name == normalizedName, ct);
        var isNew = entity is null;
        if (entity is null)
        {
            entity = new PackageEntity
            {
                Id = Guid.NewGuid(),
                OwnerUserId = userId,
                Name = normalizedName,
                Description = req.Description?.Trim() ?? string.Empty,
                Category = req.Category?.Trim() ?? string.Empty,
                RepositoryUrl = NormalizeUrl(req.RepositoryUrl),
                WebsiteUrl = NormalizeUrl(req.WebsiteUrl),
                IsPublic = req.IsPublic,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                UpdatedAtUtc = DateTimeOffset.UtcNow,
            };
            dbContext.Packages.Add(entity);
        }
        else if (entity.OwnerUserId != userId)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status403Forbidden;
            await Send.OkAsync(new UpsertPackageResponse(false, "You do not own this package namespace.", null, null), ct);
            return;
        }
        else
        {
            entity.Description = req.Description?.Trim() ?? string.Empty;
            entity.Category = req.Category?.Trim() ?? string.Empty;
            entity.RepositoryUrl = NormalizeUrl(req.RepositoryUrl);
            entity.WebsiteUrl = NormalizeUrl(req.WebsiteUrl);
            entity.IsPublic = req.IsPublic;
            entity.UpdatedAtUtc = DateTimeOffset.UtcNow;
        }

        Guid? reviewId = null;
        if (req.SubmitForReview)
        {
            var review = new PackageReviewEntity
            {
                Id = Guid.NewGuid(),
                PackageId = entity.Id,
                RequestedByUserId = userId,
                Reason = string.IsNullOrWhiteSpace(req.ReviewReason)
                    ? "Release review requested by owner."
                    : req.ReviewReason.Trim(),
                Status = "Pending",
                SubmittedAtUtc = DateTimeOffset.UtcNow,
            };

            dbContext.PackageReviews.Add(review);
            reviewId = review.Id;
        }

        var normalizedTags = (req.Tags ?? [])
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim().ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(16)
            .ToList();

        var existingTags = await dbContext.PackageTags
            .Where(x => x.PackageId == entity.Id)
            .ToListAsync(ct);
        if (existingTags.Count > 0)
        {
            dbContext.PackageTags.RemoveRange(existingTags);
        }

        if (normalizedTags.Count > 0)
        {
            dbContext.PackageTags.AddRange(normalizedTags.Select(tag => new PackageTagEntity
            {
                PackageId = entity.Id,
                Tag = tag,
                CreatedAtUtc = DateTimeOffset.UtcNow
            }));
        }

        await dbContext.SaveChangesAsync(ct);

        // If existing package was updated, notify followers
        if (!isNew)
        {
            var pId = entity.Id.ToString();
            var followerIds = await dbContext.PackageFollows
                .AsNoTracking()
                .Where(f => f.PackageId == pId)
                .Select(f => f.UserId)
                .ToListAsync(ct);

            var publisherFollowerIds = await dbContext.PublisherFollows
                .AsNoTracking()
                .Where(f => f.PublisherUserId == entity.OwnerUserId)
                .Select(f => f.UserId)
                .ToListAsync(ct);

            var targets = followerIds.Concat(publisherFollowerIds)
                .Where(uid => uid != userId)
                .Distinct()
                .ToList();

            var notifier = Resolve<Server.Services.Notifications.INotificationService>();
            foreach (var fid in targets)
            {
                await notifier.PublishAsync(fid, NotificationType.PackageUpdated,
                    $"{entity.Name} details updated",
                    $"{entity.Name} package information has been updated by the owner.",
                    preferenceScope: NotificationPreferenceScope.Package,
                    preferenceScopeId: entity.Id.ToString(),
                    ct: ct);
            }
        }

        var pendingReviewCount = await dbContext.PackageReviews
            .Where(x => x.PackageId == entity.Id && x.Status == "Pending")
            .CountAsync(ct);

        var averageRating = await dbContext.PackageCommunityReviews
            .Where(x => x.PackageId == entity.Id)
            .Select(x => (double?)x.Rating)
            .AverageAsync(ct) ?? 0d;

        var response = new UpsertPackageResponse(
            true,
            req.SubmitForReview ? "Package saved and submitted for review." : "Package saved.",
            new PackageSummaryResponse(
                entity.Id,
                entity.Name,
                entity.Description,
                entity.Category,
                entity.RepositoryUrl,
                entity.WebsiteUrl,
                normalizedTags,
                entity.IsPublic,
                entity.TotalDownloads,
                entity.UpdatedAtUtc,
                pendingReviewCount,
                Math.Round(averageRating, 2)),
            reviewId);

        await Send.OkAsync(response, ct);
    }

    private static string? NormalizeUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return Uri.TryCreate(trimmed, UriKind.Absolute, out _) ? trimmed : null;
    }
}

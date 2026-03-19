using FastEndpoints;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using pckg.Data;

namespace Server.Features.Users;

public sealed class GetPublicProfileEndpoint(
    UserManager<ApplicationUser> userManager,
    ApplicationDbContext dbContext)
    : EndpointWithoutRequest<PublicProfileResponse>
{
    public override void Configure()
    {
        Get("/users/public/{userId}");
        AllowAnonymous();
        Summary(s => s.Summary = "Get public profile for a given user id.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var userId = Route<string>("userId");
        if (string.IsNullOrWhiteSpace(userId))
        {
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await Send.OkAsync(new PublicProfileResponse(false, "User id is required.", null), ct);
            return;
        }

        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status404NotFound;
            await Send.OkAsync(new PublicProfileResponse(false, "Profile not found.", null), ct);
            return;
        }

        var displayName = string.IsNullOrWhiteSpace(user.DisplayName)
            ? user.UserName ?? "User"
            : user.DisplayName;
        var socialLinks = ProfileSocialLinks.FromUser(user);

        var packages = await dbContext.Packages
            .AsNoTracking()
            .Where(x => x.OwnerUserId == user.Id && x.IsPublic)
            .ToListAsync(ct);

        packages = packages
            .OrderByDescending(x => x.UpdatedAtUtc)
            .ToList();

        var packageIds = packages.Select(x => x.Id).ToList();

        var versionCounts = packageIds.Count == 0
            ? []
            : await dbContext.PackageVersions
                .AsNoTracking()
                .Where(x => packageIds.Contains(x.PackageId))
                .GroupBy(x => x.PackageId)
                .Select(group => new { group.Key, Count = group.Count() })
                .ToDictionaryAsync(x => x.Key, x => x.Count, ct);

        var ratingAverages = packageIds.Count == 0
            ? []
            : await dbContext.PackageCommunityReviews
                .AsNoTracking()
                .Where(x => packageIds.Contains(x.PackageId))
                .GroupBy(x => x.PackageId)
                .Select(group => new { group.Key, Average = group.Average(x => x.Rating) })
                .ToDictionaryAsync(x => x.Key, x => x.Average, ct);

        var packageSummaries = packages
            .Select(x => new PublicProfilePackageSummary(
                x.Id,
                x.Name,
                x.Description,
                x.RepositoryUrl,
                x.WebsiteUrl,
                x.TotalDownloads,
                versionCounts.GetValueOrDefault(x.Id),
                Math.Round(ratingAverages.GetValueOrDefault(x.Id), 2),
                x.UpdatedAtUtc))
            .ToList();

        var reviewCount = await dbContext.PackageCommunityReviews
            .AsNoTracking()
            .Join(
                dbContext.Packages.AsNoTracking().Where(x => x.IsPublic),
                review => review.PackageId,
                package => package.Id,
                (review, _) => review)
            .CountAsync(x => x.UserId == user.Id, ct);

        var issueCount = await dbContext.PackageIssues
            .AsNoTracking()
            .Join(
                dbContext.Packages.AsNoTracking().Where(x => x.IsPublic),
                issue => issue.PackageId,
                package => package.Id,
                (issue, _) => issue)
            .CountAsync(x => x.AuthorUserId == user.Id, ct);

        var contributorScore = await dbContext.UserRatings
            .AsNoTracking()
            .Where(x => x.UserId == user.Id)
            .Select(x => x.CalculatedScore)
            .FirstOrDefaultAsync(ct);

        var versionActivities = packageIds.Count == 0
            ? []
            : await dbContext.PackageVersions
                .AsNoTracking()
                .Where(x => packageIds.Contains(x.PackageId))
                .Join(
                    dbContext.Packages.AsNoTracking(),
                    version => version.PackageId,
                    package => package.Id,
                    (version, package) => new PublicProfileActivityItem(
                        "version",
                        package.Name,
                        $"Published version {version.Version}",
                        version.PublishedAtUtc,
                        BuildPackageLink(package.Name)))
                .ToListAsync(ct);

        versionActivities = versionActivities
            .OrderByDescending(x => x.OccurredAtUtc)
            .Take(6)
            .ToList();

        var reviewActivities = await dbContext.PackageCommunityReviews
            .AsNoTracking()
            .Where(x => x.UserId == user.Id)
            .Join(
                dbContext.Packages.AsNoTracking().Where(x => x.IsPublic),
                review => review.PackageId,
                package => package.Id,
                (review, package) => new PublicProfileActivityItem(
                    "review",
                    package.Name,
                    $"Reviewed this package ({review.Rating}/5): {Truncate(review.Comment, 88)}",
                    review.CreatedAtUtc,
                    BuildPackageLink(package.Name)))
            .ToListAsync(ct);

        reviewActivities = reviewActivities
            .OrderByDescending(x => x.OccurredAtUtc)
            .Take(6)
            .ToList();

        var issueActivities = await dbContext.PackageIssues
            .AsNoTracking()
            .Where(x => x.AuthorUserId == user.Id)
            .Join(
                dbContext.Packages.AsNoTracking().Where(x => x.IsPublic),
                issue => issue.PackageId,
                package => package.Id,
                (issue, package) => new PublicProfileActivityItem(
                    "issue",
                    package.Name,
                    $"Opened issue: {Truncate(issue.Title, 72)}",
                    issue.CreatedAtUtc,
                    BuildPackageLink(package.Name)))
            .ToListAsync(ct);

        issueActivities = issueActivities
            .OrderByDescending(x => x.OccurredAtUtc)
            .Take(6)
            .ToList();

        var recentActivity = versionActivities
            .Concat(reviewActivities)
            .Concat(issueActivities)
            .OrderByDescending(x => x.OccurredAtUtc)
            .Take(10)
            .ToList();

        var stats = new PublicProfileStats(
            packageSummaries.Count,
            versionCounts.Values.Sum(),
            reviewCount,
            issueCount,
            packages.Sum(x => x.TotalDownloads),
            Math.Round(contributorScore, 1),
            packageSummaries.Count == 0 ? 0d : Math.Round(packageSummaries.Average(x => x.AverageRating), 2));

        await Send.OkAsync(
            new PublicProfileResponse(
                true,
                "Profile loaded.",
                new PublicProfilePayload(
                    user.Id,
                    displayName,
                    string.IsNullOrWhiteSpace(user.Bio) ? null : user.Bio,
                    string.IsNullOrWhiteSpace(ProfileSocialLinks.GetLegacyUrl(socialLinks, Components.Shared.SocialPlatform.GitHub)) ? null : ProfileSocialLinks.GetLegacyUrl(socialLinks, Components.Shared.SocialPlatform.GitHub),
                    string.IsNullOrWhiteSpace(ProfileSocialLinks.GetLegacyUrl(socialLinks, Components.Shared.SocialPlatform.Website)) ? null : ProfileSocialLinks.GetLegacyUrl(socialLinks, Components.Shared.SocialPlatform.Website),
                    string.IsNullOrWhiteSpace(ProfileSocialLinks.GetLegacyUrl(socialLinks, Components.Shared.SocialPlatform.X)) ? null : ProfileSocialLinks.GetLegacyUrl(socialLinks, Components.Shared.SocialPlatform.X),
                    socialLinks,
                    string.IsNullOrWhiteSpace(user.ProfileImageUrl) ? null : user.ProfileImageUrl,
                    stats,
                    packageSummaries,
                    recentActivity)),
            ct);
    }

    private static string BuildPackageLink(string packageName)
        => $"/packages/{Uri.EscapeDataString(packageName)}";

    private static string Truncate(string value, int maxLength)
    {
        var trimmed = value.Trim();
        if (trimmed.Length <= maxLength)
        {
            return trimmed;
        }

        return string.Concat(trimmed.AsSpan(0, maxLength - 1), "…");
    }
}

public sealed record PublicProfileResponse(bool Success, string Message, PublicProfilePayload? Profile);

public sealed record PublicProfilePayload(
    string UserId,
    string DisplayName,
    string? Bio,
    string? GitHubUrl,
    string? WebsiteUrl,
    string? XUrl,
    IReadOnlyList<ProfileSocialLink> SocialLinks,
    string? ProfileImageUrl,
    PublicProfileStats Stats,
    IReadOnlyList<PublicProfilePackageSummary> Packages,
    IReadOnlyList<PublicProfileActivityItem> RecentActivity);

public sealed record PublicProfileStats(
    int PackageCount,
    int PublishedVersionCount,
    int ReviewCount,
    int IssueCount,
    long TotalDownloads,
    double ContributorScore,
    double AveragePackageRating);

public sealed record PublicProfilePackageSummary(
    Guid Id,
    string Name,
    string Description,
    string? RepositoryUrl,
    string? WebsiteUrl,
    long TotalDownloads,
    int VersionCount,
    double AverageRating,
    DateTimeOffset UpdatedAtUtc);

public sealed record PublicProfileActivityItem(
    string Kind,
    string Label,
    string Detail,
    DateTimeOffset OccurredAtUtc,
    string? LinkUrl);

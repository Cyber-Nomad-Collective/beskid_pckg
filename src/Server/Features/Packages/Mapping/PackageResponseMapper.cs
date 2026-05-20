using Server.Data;
using Server.Services;

namespace Server.Features.Packages.Mapping;

public static class PackageResponseMapper
{
    public static PackageVersionSummaryResponse ToVersionSummary(PackageVersionEntity entity, string packageName)
        => new(
            entity.Id,
            entity.PackageId,
            packageName,
            entity.Version,
            entity.IsYanked,
            entity.ChecksumSha256,
            entity.SizeBytes,
            entity.PublishedAtUtc,
            entity.YankedAtUtc,
            !string.IsNullOrWhiteSpace(entity.ReadmeMarkdown),
            entity.ConfigurationJson,
            entity.OverridesJson);

    public static PackageSummaryResponse ToSummary(
        PackageEntity package,
        IReadOnlyList<string> tags,
        int pendingReviewsCount,
        double averageRating,
        PublisherOwnerRow owner)
    {
        var ownerDisplay = string.IsNullOrWhiteSpace(owner.DisplayLabel) ? package.OwnerUserId : owner.DisplayLabel;
        return new PackageSummaryResponse(
            package.Id,
            package.Name,
            package.Description,
            package.Category,
            package.RepositoryUrl,
            package.WebsiteUrl,
            tags,
            package.IsPublic,
            package.TotalDownloads,
            package.UpdatedAtUtc,
            pendingReviewsCount,
            Math.Round(averageRating, 2),
            package.IconUrl,
            package.OwnerUserId,
            ownerDisplay,
            owner.IsPublisherVerified);
    }

    public static PackageHealthSnapshotResponse ToHealthSnapshot(PackageHealthStatus health)
        => new(
            health.State,
            health.SubState,
            health.Score,
            health.UpdateRate.State,
            health.UpdateRate.SubState,
            health.UpdateRate.Normalized,
            health.UpdateRate.Weight,
            health.Downloads.State,
            health.Downloads.SubState,
            health.Downloads.Normalized,
            health.Downloads.Weight,
            health.Reviews.State,
            health.Reviews.SubState,
            health.Reviews.Normalized,
            health.Reviews.Weight);
}

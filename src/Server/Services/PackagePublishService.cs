using Microsoft.EntityFrameworkCore;
using Server.Data;
using Server.Features.Packages;
using Server.Features.Packages.Mapping;
using Server.Services.Notifications;

namespace Server.Services;

public sealed record PackagePublishRequest(
    PackageEntity Package,
    string Version,
    Stream ArtifactStream,
    bool RelaxPackageJsonVersion,
    string? ExpectedChecksum,
    string ContentType,
    string UserId);

public sealed record PackagePublishResult(
    bool Success,
    string Message,
    PackageVersionSummaryResponse? Version,
    int StatusCode);

public interface IPackagePublishService
{
    Task<PackagePublishResult> PublishAsync(
        PackagePublishRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class PackagePublishService(
    ApplicationDbContext dbContext,
    IPackageArtifactStore artifactStore,
    IPackageArtifactValidator artifactValidator,
    IPackageArtifactPublishMetadataExtractor publishMetadataExtractor,
    INotificationService notifications,
    ILogger<PackagePublishService> logger) : IPackagePublishService
{
    public async Task<PackagePublishResult> PublishAsync(
        PackagePublishRequest request,
        CancellationToken cancellationToken = default)
    {
        var package = request.Package;
        var version = request.Version;

        var existing = await dbContext.PackageVersions
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.PackageId == package.Id && x.Version == version, cancellationToken);
        if (existing is not null)
        {
            if (!string.IsNullOrWhiteSpace(request.ExpectedChecksum)
                && string.Equals(existing.ChecksumSha256, request.ExpectedChecksum, StringComparison.OrdinalIgnoreCase))
            {
                return Success(
                    "Package version already exists with matching checksum.",
                    ToSummary(existing, package.Name));
            }

            return Failure("Version already exists and is immutable.", StatusCodes.Status409Conflict);
        }

        await using var artifactStream = request.ArtifactStream;
        var validation = await artifactValidator.ValidateAsync(
            artifactStream,
            package.Name,
            version,
            request.RelaxPackageJsonVersion,
            cancellationToken);
        if (!validation.IsValid)
        {
            return Failure(validation.Message, StatusCodes.Status400BadRequest);
        }

        if (!string.IsNullOrWhiteSpace(request.ExpectedChecksum)
            && !string.Equals(request.ExpectedChecksum, validation.ArtifactChecksumSha256, StringComparison.OrdinalIgnoreCase))
        {
            return Failure("Checksum mismatch for uploaded artifact.", StatusCodes.Status400BadRequest);
        }

        artifactStream.Position = 0;
        var publishMetadata = publishMetadataExtractor.Extract(
            artifactStream,
            validation.ManifestJson ?? "{}");

        var normalizedIconUrl = PackageRegistryUrlNormalizer.NormalizeIconUrl(publishMetadata.IconUrl);
        if (normalizedIconUrl is not null)
        {
            package.IconUrl = normalizedIconUrl;
        }

        artifactStream.Position = 0;
        var (storageKey, computedChecksum, sizeBytes) = await artifactStore.SaveAsync(
            package.Name,
            version,
            artifactStream,
            cancellationToken);

        if (!string.Equals(computedChecksum, validation.ArtifactChecksumSha256, StringComparison.OrdinalIgnoreCase))
        {
            logger.LogError(
                "Publish failed after persistence: checksum verification for {PackageName} {Version}.",
                package.Name,
                version);
            return Failure(
                "Artifact checksum could not be verified after persistence.",
                StatusCodes.Status500InternalServerError);
        }

        var entity = new PackageVersionEntity
        {
            Id = Guid.NewGuid(),
            PackageId = package.Id,
            Version = version,
            ManifestJson = validation.ManifestJson ?? "{}",
            ReadmeMarkdown = publishMetadata.ReadmeMarkdown,
            ConfigurationJson = publishMetadata.ConfigurationJson,
            OverridesJson = publishMetadata.OverridesJson,
            ChecksumSha256 = computedChecksum,
            StorageKey = storageKey,
            ContentType = string.IsNullOrWhiteSpace(request.ContentType) ? "application/zip" : request.ContentType,
            SizeBytes = sizeBytes,
            PublishedAtUtc = DateTimeOffset.UtcNow,
        };

        dbContext.PackageVersions.Add(entity);
        package.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        await notifications.PublishAsync(
            request.UserId,
            NotificationType.PackagePublished,
            $"{package.Name} {entity.Version} published",
            $"Your package {package.Name} has been published with version {entity.Version}.",
            ct: cancellationToken);

        var pId = package.Id.ToString();
        var packageFollowerIds = await dbContext.PackageFollows
            .AsNoTracking()
            .Where(f => f.PackageId == pId)
            .Select(f => f.UserId)
            .ToListAsync(cancellationToken);

        var publisherFollowerIds = await dbContext.PublisherFollows
            .AsNoTracking()
            .Where(f => f.PublisherUserId == package.OwnerUserId)
            .Select(f => f.UserId)
            .ToListAsync(cancellationToken);

        foreach (var followerId in packageFollowerIds
                     .Concat(publisherFollowerIds)
                     .Where(uid => uid != request.UserId)
                     .Distinct())
        {
            await notifications.PublishAsync(
                followerId,
                NotificationType.PackagePublished,
                $"{package.Name} {entity.Version} published",
                $"{package.Name} has a new version {entity.Version}.",
                preferenceScope: NotificationPreferenceScope.Package,
                preferenceScopeId: package.Id.ToString(),
                ct: cancellationToken);
        }

        return Success("Package version published.", ToSummary(entity, package.Name));
    }

    private static PackagePublishResult Success(string message, PackageVersionSummaryResponse version)
        => new(true, message, version, StatusCodes.Status200OK);

    private static PackagePublishResult Failure(string message, int statusCode)
        => new(false, message, null, statusCode);

    private static PackageVersionSummaryResponse ToSummary(PackageVersionEntity entity, string packageName)
        => PackageResponseMapper.ToVersionSummary(entity, packageName);
}

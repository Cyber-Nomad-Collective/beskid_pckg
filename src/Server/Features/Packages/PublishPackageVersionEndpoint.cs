using System.Collections.Generic;
using System.Text.RegularExpressions;
using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using Server.Data;
using Server.Services;

namespace Server.Features.Packages;

public sealed class PublishPackageVersionEndpoint(
    ApplicationDbContext dbContext,
    IApiPrincipalResolver principalResolver,
    IPackageArtifactStore artifactStore,
    IPackageArtifactValidator artifactValidator,
    Server.Services.Notifications.INotificationService notifications,
    IPckgRegistryActivityLog registryActivity,
    ILogger<PublishPackageVersionEndpoint> logger)
    : EndpointWithoutRequest<PublishPackageVersionResponse>
{
    private const long MaxArtifactSizeBytes = 64 * 1024 * 1024;
    private static readonly Regex SemVerRegex = new(
        @"^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(?:-[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*)?(?:\+[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*)?$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public override void Configure()
    {
        Post("/packages/{PackageName}/publish");
        Options(x =>
        {
            x.RequireAuthorization();
            x.RequireRateLimiting("publish");
        });
        Summary(s => s.Summary = "Publish a package artifact version for an owned package.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        using var logScope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["TraceId"] = HttpContext.TraceIdentifier,
        });

        void Record(string severity, string action, string message, string? userId, string? packageName = null, string? version = null)
        {
            registryActivity.Append(new RegistryActivityEntry(
                DateTimeOffset.UtcNow,
                severity,
                action,
                message,
                HttpContext.TraceIdentifier,
                userId,
                packageName,
                version));
        }

        var userId = await principalResolver.ResolveUserIdAsync(HttpContext, ct);
        if (string.IsNullOrWhiteSpace(userId))
        {
            logger.LogWarning("Publish rejected: unauthenticated caller.");
            Record("Warning", "publish", "Unauthorized.", null, Route<string>("PackageName")?.Trim(), null);
            HttpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await Send.OkAsync(new PublishPackageVersionResponse(false, "Unauthorized.", null), ct);
            return;
        }

        using var userScope = logger.BeginScope(new Dictionary<string, object?> { ["UserId"] = userId });

        var packageName = Route<string>("PackageName")?.Trim();
        if (string.IsNullOrWhiteSpace(packageName))
        {
            logger.LogWarning("Publish rejected: missing package name.");
            Record("Warning", "publish", "Package name is required.", userId, null, null);
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await Send.OkAsync(new PublishPackageVersionResponse(false, "Package name is required.", null), ct);
            return;
        }

        var package = await dbContext.Packages.SingleOrDefaultAsync(x => x.Name == packageName, ct);
        if (package is null)
        {
            logger.LogWarning("Publish rejected: package {PackageName} not found.", packageName);
            Record("Warning", "publish", "Package was not found.", userId, packageName, null);
            HttpContext.Response.StatusCode = StatusCodes.Status404NotFound;
            await Send.OkAsync(new PublishPackageVersionResponse(false, "Package was not found.", null), ct);
            return;
        }

        if (package.OwnerUserId != userId)
        {
            logger.LogWarning(
                "Publish rejected: user {UserId} does not own package {PackageName}.",
                userId,
                packageName);
            Record("Warning", "publish", "You do not own this package.", userId, packageName, null);
            HttpContext.Response.StatusCode = StatusCodes.Status403Forbidden;
            await Send.OkAsync(new PublishPackageVersionResponse(false, "You do not own this package.", null), ct);
            return;
        }

        if (!HttpContext.Request.HasFormContentType)
        {
            logger.LogWarning("Publish rejected: expected multipart form for {PackageName}.", packageName);
            Record("Warning", "publish", "Expected multipart form payload.", userId, packageName, null);
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await Send.OkAsync(new PublishPackageVersionResponse(false, "Expected multipart form payload.", null), ct);
            return;
        }

        var form = await HttpContext.Request.ReadFormAsync(ct);
        var version = form["version"].FirstOrDefault()?.Trim();
        var expectedChecksum = form["checksumSha256"].FirstOrDefault()?.Trim().ToLowerInvariant();
        var inlineManifestJson = form["manifestJson"].FirstOrDefault();
        var artifact = form.Files.GetFile("artifact");

        if (string.IsNullOrWhiteSpace(version) || artifact is null)
        {
            logger.LogWarning("Publish rejected: missing version or artifact for {PackageName}.", packageName);
            Record("Warning", "publish", "Both version and artifact are required.", userId, packageName, version);
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await Send.OkAsync(new PublishPackageVersionResponse(false, "Both version and artifact are required.", null), ct);
            return;
        }

        if (!SemVerRegex.IsMatch(version))
        {
            logger.LogWarning("Publish rejected: invalid semver {Version} for {PackageName}.", version, packageName);
            Record("Warning", "publish", "Version must be a valid semantic version.", userId, packageName, version);
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await Send.OkAsync(new PublishPackageVersionResponse(false, "Version must be a valid semantic version.", null), ct);
            return;
        }

        if (artifact.Length <= 0 || artifact.Length > MaxArtifactSizeBytes)
        {
            logger.LogWarning(
                "Publish rejected: artifact size {ArtifactLength} invalid for {PackageName} {Version}.",
                artifact.Length,
                packageName,
                version);
            Record("Warning", "publish", $"Artifact size must be between 1 byte and {MaxArtifactSizeBytes} bytes.", userId, packageName, version);
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await Send.OkAsync(
                new PublishPackageVersionResponse(false, $"Artifact size must be between 1 byte and {MaxArtifactSizeBytes} bytes.", null),
                ct);
            return;
        }

        var existing = await dbContext.PackageVersions
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.PackageId == package.Id && x.Version == version, ct);
        if (existing is not null)
        {
            if (!string.IsNullOrWhiteSpace(expectedChecksum)
                && string.Equals(existing.ChecksumSha256, expectedChecksum, StringComparison.OrdinalIgnoreCase))
            {
                logger.LogInformation(
                    "Idempotent publish accepted for package {PackageName} version {Version} (trace {TraceId}).",
                    package.Name,
                    version,
                    HttpContext.TraceIdentifier);
                Record("Information", "publish_idempotent", "Package version already exists with matching checksum.", userId, package.Name, version);
                await Send.OkAsync(
                    new PublishPackageVersionResponse(
                        true,
                        "Package version already exists with matching checksum.",
                        new PackageVersionSummaryResponse(
                            existing.Id,
                            existing.PackageId,
                            package.Name,
                            existing.Version,
                            existing.IsYanked,
                            existing.ChecksumSha256,
                            existing.SizeBytes,
                            existing.PublishedAtUtc,
                            existing.YankedAtUtc)),
                    ct);
                return;
            }

            logger.LogWarning("Publish rejected: immutable version conflict for {PackageName} {Version}.", package.Name, version);
            Record("Warning", "publish_conflict", "Version already exists and is immutable.", userId, package.Name, version);
            HttpContext.Response.StatusCode = StatusCodes.Status409Conflict;
            await Send.OkAsync(new PublishPackageVersionResponse(false, "Version already exists and is immutable.", null), ct);
            return;
        }

        await using var artifactStream = artifact.OpenReadStream();
        var validation = await artifactValidator.ValidateAsync(artifactStream, package.Name, version, ct);
        if (!validation.IsValid)
        {
            logger.LogWarning(
                "Publish rejected: artifact validation failed for {PackageName} {Version}: {ValidationMessage}",
                package.Name,
                version,
                validation.Message);
            Record("Warning", "publish_validation_failed", validation.Message, userId, package.Name, version);
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await Send.OkAsync(new PublishPackageVersionResponse(false, validation.Message, null), ct);
            return;
        }

        if (!string.IsNullOrWhiteSpace(expectedChecksum)
            && !string.Equals(expectedChecksum, validation.ArtifactChecksumSha256, StringComparison.OrdinalIgnoreCase))
        {
            logger.LogWarning("Publish rejected: checksum mismatch for {PackageName} {Version}.", package.Name, version);
            Record("Warning", "publish_checksum_mismatch", "Checksum mismatch for uploaded artifact.", userId, package.Name, version);
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await Send.OkAsync(new PublishPackageVersionResponse(false, "Checksum mismatch for uploaded artifact.", null), ct);
            return;
        }

        artifactStream.Position = 0;
        var (storageKey, computedChecksum, sizeBytes) = await artifactStore.SaveAsync(package.Name, version, artifactStream, ct);

        if (!string.IsNullOrWhiteSpace(inlineManifestJson))
        {
            logger.LogDebug(
                "Publish payload included optional manifestJson for {PackageName} {Version}; server persisted manifest from artifact.",
                package.Name,
                version);
        }

        if (!string.Equals(computedChecksum, validation.ArtifactChecksumSha256, StringComparison.OrdinalIgnoreCase))
        {
            logger.LogError(
                "Publish failed after persistence: checksum verification for {PackageName} {Version}.",
                package.Name,
                version);
            Record("Error", "publish_persist_checksum", "Artifact checksum could not be verified after persistence.", userId, package.Name, version);
            HttpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await Send.OkAsync(new PublishPackageVersionResponse(false, "Artifact checksum could not be verified after persistence.", null), ct);
            return;
        }

        var entity = new PackageVersionEntity
        {
            Id = Guid.NewGuid(),
            PackageId = package.Id,
            Version = version,
            ManifestJson = validation.ManifestJson ?? "{}",
            ChecksumSha256 = computedChecksum,
            StorageKey = storageKey,
            ContentType = string.IsNullOrWhiteSpace(artifact.ContentType) ? "application/zip" : artifact.ContentType,
            SizeBytes = sizeBytes,
            PublishedAtUtc = DateTimeOffset.UtcNow,
        };

        dbContext.PackageVersions.Add(entity);
        package.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(ct);
        logger.LogInformation(
            "Published package {PackageName} version {Version} by user {UserId}; sizeBytes={SizeBytes}; trace={TraceId}.",
            package.Name,
            entity.Version,
            userId,
            sizeBytes,
            HttpContext.TraceIdentifier);
        Record(
            "Information",
            "publish_success",
            $"Published version {entity.Version} ({sizeBytes} bytes).",
            userId,
            package.Name,
            entity.Version);

        await notifications.PublishAsync(userId, NotificationType.PackagePublished,
            $"{package.Name} {entity.Version} published",
            $"Your package {package.Name} has been published with version {entity.Version}.", ct: ct);

        var pId = package.Id.ToString();
        var packageFollowerIds = await dbContext.PackageFollows
            .AsNoTracking()
            .Where(f => f.PackageId == pId)
            .Select(f => f.UserId)
            .ToListAsync(ct);

        var publisherFollowerIds = await dbContext.PublisherFollows
            .AsNoTracking()
            .Where(f => f.PublisherUserId == package.OwnerUserId)
            .Select(f => f.UserId)
            .ToListAsync(ct);

        var targetFollowerIds = packageFollowerIds
            .Concat(publisherFollowerIds)
            .Where(uid => uid != userId)
            .Distinct()
            .ToList();

        foreach (var fid in targetFollowerIds)
        {
            await notifications.PublishAsync(fid, NotificationType.PackagePublished,
                $"{package.Name} {entity.Version} published",
                $"{package.Name} has a new version {entity.Version}.",
                preferenceScope: NotificationPreferenceScope.Package,
                preferenceScopeId: package.Id.ToString(),
                ct: ct);
        }

        var response = new PackageVersionSummaryResponse(
            entity.Id,
            entity.PackageId,
            package.Name,
            entity.Version,
            entity.IsYanked,
            entity.ChecksumSha256,
            entity.SizeBytes,
            entity.PublishedAtUtc,
            entity.YankedAtUtc);

        await Send.OkAsync(new PublishPackageVersionResponse(true, "Package version published.", response), ct);
    }
}

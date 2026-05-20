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
    IPackagePublishService packagePublishService,
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
        Summary(s =>
        {
            s.Summary = "Publish a package artifact version for an owned package.";
            s.Description =
                "Multipart: artifact (required), version (optional — omit for registry-assigned next semver), " +
                "versionBump (optional patch|minor|major when version omitted, default patch), checksumSha256 (optional).";
        });
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        using var logScope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["TraceId"] = HttpContext.TraceIdentifier,
        });

        async Task RecordAsync(string severity, string action, string message, string? userId, string? packageName = null, string? version = null)
        {
            await registryActivity.AppendAsync(new RegistryActivityEntry(
                DateTimeOffset.UtcNow,
                severity,
                action,
                message,
                HttpContext.TraceIdentifier,
                userId,
                packageName,
                version), ct);
        }

        var userId = await principalResolver.ResolveUserIdAsync(HttpContext, ct);
        if (string.IsNullOrWhiteSpace(userId))
        {
            logger.LogWarning("Publish rejected: unauthenticated caller.");
            await RecordAsync("Warning", "publish", "Unauthorized.", null, Route<string>("PackageName")?.Trim(), null);
            await Send.ResponseAsync(new PublishPackageVersionResponse(false, "Unauthorized.", null), StatusCodes.Status401Unauthorized, ct);
            return;
        }

        using var userScope = logger.BeginScope(new Dictionary<string, object?> { ["UserId"] = userId });

        var packageName = Route<string>("PackageName")?.Trim();
        if (string.IsNullOrWhiteSpace(packageName))
        {
            logger.LogWarning("Publish rejected: missing package name.");
            await RecordAsync("Warning", "publish", "Package name is required.", userId, null, null);
            await Send.ResponseAsync(new PublishPackageVersionResponse(false, "Package name is required.", null), StatusCodes.Status400BadRequest, ct);
            return;
        }

        var package = await dbContext.Packages.SingleOrDefaultAsync(x => x.Name == packageName, ct);
        if (package is null)
        {
            logger.LogWarning("Publish rejected: package {PackageName} not found.", packageName);
            await RecordAsync("Warning", "publish", "Package was not found.", userId, packageName, null);
            await Send.ResponseAsync(new PublishPackageVersionResponse(false, "Package was not found.", null), StatusCodes.Status404NotFound, ct);
            return;
        }

        if (package.OwnerUserId != userId)
        {
            logger.LogWarning(
                "Publish rejected: user {UserId} does not own package {PackageName}.",
                userId,
                packageName);
            await RecordAsync("Warning", "publish", "You do not own this package.", userId, packageName, null);
            await Send.ResponseAsync(new PublishPackageVersionResponse(false, "You do not own this package.", null), StatusCodes.Status403Forbidden, ct);
            return;
        }

        if (!HttpContext.Request.HasFormContentType)
        {
            logger.LogWarning("Publish rejected: expected multipart form for {PackageName}.", packageName);
            await RecordAsync("Warning", "publish", "Expected multipart form payload.", userId, packageName, null);
            await Send.ResponseAsync(new PublishPackageVersionResponse(false, "Expected multipart form payload.", null), StatusCodes.Status400BadRequest, ct);
            return;
        }

        var form = await HttpContext.Request.ReadFormAsync(ct);
        var versionRaw = form["version"].FirstOrDefault()?.Trim();
        var versionBumpRaw = form["versionBump"].FirstOrDefault()?.Trim();
        var expectedChecksum = form["checksumSha256"].FirstOrDefault()?.Trim().ToLowerInvariant();
        var artifact = form.Files.GetFile("artifact");

        if (artifact is null)
        {
            logger.LogWarning("Publish rejected: missing artifact for {PackageName}.", packageName);
            await RecordAsync("Warning", "publish", "Artifact file is required.", userId, packageName, null);
            await Send.ResponseAsync(new PublishPackageVersionResponse(false, "Artifact file is required.", null), StatusCodes.Status400BadRequest, ct);
            return;
        }

        var relaxPackageJsonVersion = false;
        string version;
        if (string.IsNullOrWhiteSpace(versionRaw))
        {
            var bump = PackageVersioning.ParseBump(versionBumpRaw);
            var nonYankedVersions = await dbContext.PackageVersions
                .AsNoTracking()
                .Where(x => x.PackageId == package.Id && !x.IsYanked)
                .Select(x => x.Version)
                .ToListAsync(ct);
            version = PackageVersioning.ComputeNextVersion(nonYankedVersions, bump);
            relaxPackageJsonVersion = true;
        }
        else
        {
            version = versionRaw;
        }

        if (!SemVerRegex.IsMatch(version))
        {
            logger.LogWarning("Publish rejected: invalid semver {Version} for {PackageName}.", version, packageName);
            await RecordAsync("Warning", "publish", "Version must be a valid semantic version.", userId, packageName, version);
            await Send.ResponseAsync(new PublishPackageVersionResponse(false, "Version must be a valid semantic version.", null), StatusCodes.Status400BadRequest, ct);
            return;
        }

        if (artifact.Length <= 0 || artifact.Length > MaxArtifactSizeBytes)
        {
            logger.LogWarning(
                "Publish rejected: artifact size {ArtifactLength} invalid for {PackageName} {Version}.",
                artifact.Length,
                packageName,
                version);
            await RecordAsync("Warning", "publish", $"Artifact size must be between 1 byte and {MaxArtifactSizeBytes} bytes.", userId, packageName, version);
            await Send.ResponseAsync(
                new PublishPackageVersionResponse(false, $"Artifact size must be between 1 byte and {MaxArtifactSizeBytes} bytes.", null),
                StatusCodes.Status400BadRequest,
                ct);
            return;
        }

        await using var artifactStream = artifact.OpenReadStream();
        var publishResult = await packagePublishService.PublishAsync(
            new PackagePublishRequest(
                package,
                version,
                artifactStream,
                relaxPackageJsonVersion,
                expectedChecksum,
                artifact.ContentType ?? "application/zip",
                userId),
            ct);

        if (!publishResult.Success)
        {
            await RecordAsync("Warning", "publish_failed", publishResult.Message, userId, package.Name, version);
            await Send.ResponseAsync(
                new PublishPackageVersionResponse(false, publishResult.Message, null),
                publishResult.StatusCode,
                ct);
            return;
        }

        logger.LogInformation(
            "Published package {PackageName} version {Version} by user {UserId}; trace={TraceId}.",
            package.Name,
            publishResult.Version!.Version,
            userId,
            HttpContext.TraceIdentifier);
        await RecordAsync(
            "Information",
            "publish_success",
            $"Published version {publishResult.Version.Version}.",
            userId,
            package.Name,
            publishResult.Version.Version);

        await Send.OkAsync(
            new PublishPackageVersionResponse(true, publishResult.Message, publishResult.Version),
            ct);
    }
}

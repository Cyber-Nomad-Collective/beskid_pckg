using System.Collections.Generic;
using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using Server.Data;
using Server.Services;

namespace Server.Features.Packages;

public sealed class UnyankPackageVersionEndpoint(
    ApplicationDbContext dbContext,
    IApiPrincipalResolver principalResolver,
    IPckgRegistryActivityLog registryActivity,
    ILogger<UnyankPackageVersionEndpoint> logger)
    : EndpointWithoutRequest<PackageVersionLifecycleResponse>
{
    public override void Configure()
    {
        Post("/packages/{PackageName}/versions/{Version}/unyank");
        Options(x => x.RequireAuthorization());
        Summary(s => s.Summary = "Restore a yanked package version.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        using var logScope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["TraceId"] = HttpContext.TraceIdentifier,
        });

        void Record(string severity, string action, string message, string? uid, string? pkg, string? ver)
        {
            registryActivity.Append(new RegistryActivityEntry(
                DateTimeOffset.UtcNow,
                severity,
                action,
                message,
                HttpContext.TraceIdentifier,
                uid,
                pkg,
                ver));
        }

        var userId = await principalResolver.ResolveUserIdAsync(HttpContext, ct);
        if (string.IsNullOrWhiteSpace(userId))
        {
            logger.LogWarning("Unyank rejected: unauthenticated.");
            Record("Warning", "unyank", "Unauthorized.", null, null, null);
            HttpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await Send.OkAsync(new PackageVersionLifecycleResponse(false, "Unauthorized.", null), ct);
            return;
        }

        var packageName = Route<string>("PackageName")?.Trim();
        var version = Route<string>("Version")?.Trim();
        if (string.IsNullOrWhiteSpace(packageName) || string.IsNullOrWhiteSpace(version))
        {
            logger.LogWarning("Unyank rejected: missing route parameters.");
            Record("Warning", "unyank", "Package name and version are required.", userId, packageName, version);
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await Send.OkAsync(new PackageVersionLifecycleResponse(false, "Package name and version are required.", null), ct);
            return;
        }

        var package = await dbContext.Packages.SingleOrDefaultAsync(x => x.Name == packageName, ct);
        if (package is null)
        {
            logger.LogWarning("Unyank rejected: package {PackageName} not found.", packageName);
            Record("Warning", "unyank", "Package was not found.", userId, packageName, version);
            HttpContext.Response.StatusCode = StatusCodes.Status404NotFound;
            await Send.OkAsync(new PackageVersionLifecycleResponse(false, "Package was not found.", null), ct);
            return;
        }

        if (package.OwnerUserId != userId && !User.IsInRole("SuperAdmin"))
        {
            logger.LogWarning("Unyank rejected: forbidden for user {UserId} on {PackageName}.", userId, packageName);
            Record("Warning", "unyank", "You do not have permission to unyank this version.", userId, packageName, version);
            HttpContext.Response.StatusCode = StatusCodes.Status403Forbidden;
            await Send.OkAsync(new PackageVersionLifecycleResponse(false, "You do not have permission to unyank this version.", null), ct);
            return;
        }

        var entity = await dbContext.PackageVersions
            .SingleOrDefaultAsync(x => x.PackageId == package.Id && x.Version == version, ct);
        if (entity is null)
        {
            logger.LogWarning("Unyank rejected: version {Version} not found for {PackageName}.", version, packageName);
            Record("Warning", "unyank", "Version was not found.", userId, packageName, version);
            HttpContext.Response.StatusCode = StatusCodes.Status404NotFound;
            await Send.OkAsync(new PackageVersionLifecycleResponse(false, "Version was not found.", null), ct);
            return;
        }

        if (!entity.IsYanked)
        {
            logger.LogWarning("Unyank rejected: version already active {PackageName} {Version}.", packageName, version);
            Record("Warning", "unyank_conflict", "Version is already active.", userId, packageName, version);
            HttpContext.Response.StatusCode = StatusCodes.Status409Conflict;
            await Send.OkAsync(new PackageVersionLifecycleResponse(false, "Version is already active.", ToResponse(entity, package.Name)), ct);
            return;
        }

        entity.IsYanked = false;
        entity.YankedAtUtc = null;
        package.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(ct);

        logger.LogInformation("Unyanked {PackageName} {Version} by {UserId}; trace={TraceId}.", packageName, version, userId, HttpContext.TraceIdentifier);
        Record("Information", "unyank_success", "Version restored.", userId, packageName, version);

        await Send.OkAsync(new PackageVersionLifecycleResponse(true, "Version restored.", ToResponse(entity, package.Name)), ct);
    }

    private static PackageVersionSummaryResponse ToResponse(PackageVersionEntity entity, string packageName)
        => new(
            entity.Id,
            entity.PackageId,
            packageName,
            entity.Version,
            entity.IsYanked,
            entity.ChecksumSha256,
            entity.SizeBytes,
            entity.PublishedAtUtc,
            entity.YankedAtUtc);
}

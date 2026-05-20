using Microsoft.EntityFrameworkCore;
using Server.Data;
using Server.Features.Packages.Mapping;
using Server.Services;

namespace Server.Features.Packages;

public sealed record PackageVersionLifecycleResult(int StatusCode, PackageVersionLifecycleResponse Body);

public interface IPackageVersionLifecycleService
{
    Task<PackageVersionLifecycleResult> SetYankedAsync(
        HttpContext httpContext,
        string packageName,
        string version,
        bool yanked,
        CancellationToken cancellationToken = default);
}

public sealed class PackageVersionLifecycleService(
    ApplicationDbContext dbContext,
    IApiPrincipalResolver principalResolver,
    IPckgRegistryActivityLog registryActivity,
    ILogger<PackageVersionLifecycleService> logger) : IPackageVersionLifecycleService
{
    public async Task<PackageVersionLifecycleResult> SetYankedAsync(
        HttpContext httpContext,
        string packageName,
        string version,
        bool yanked,
        CancellationToken cancellationToken = default)
    {
        var action = yanked ? "yank" : "unyank";
        var userId = await principalResolver.ResolveUserIdAsync(httpContext, cancellationToken);
        if (string.IsNullOrWhiteSpace(userId))
        {
            await RecordAsync(action, "Warning", "Unauthorized.", null, null, null, httpContext);
            return new(
                StatusCodes.Status401Unauthorized,
                new PackageVersionLifecycleResponse(false, "Unauthorized.", null));
        }

        if (string.IsNullOrWhiteSpace(packageName) || string.IsNullOrWhiteSpace(version))
        {
            await RecordAsync(action, "Warning", "Package name and version are required.", userId, packageName, version, httpContext);
            return new(
                StatusCodes.Status400BadRequest,
                new PackageVersionLifecycleResponse(false, "Package name and version are required.", null));
        }

        var package = await dbContext.Packages.SingleOrDefaultAsync(x => x.Name == packageName, cancellationToken);
        if (package is null)
        {
            await RecordAsync(action, "Warning", "Package was not found.", userId, packageName, version, httpContext);
            return new(
                StatusCodes.Status404NotFound,
                new PackageVersionLifecycleResponse(false, "Package was not found.", null));
        }

        if (package.OwnerUserId != userId && !httpContext.User.IsInRole("SuperAdmin"))
        {
            await RecordAsync(action, "Warning", $"You do not have permission to {action} this version.", userId, packageName, version, httpContext);
            return new(
                StatusCodes.Status403Forbidden,
                new PackageVersionLifecycleResponse(false, $"You do not have permission to {action} this version.", null));
        }

        var entity = await dbContext.PackageVersions
            .SingleOrDefaultAsync(x => x.PackageId == package.Id && x.Version == version, cancellationToken);
        if (entity is null)
        {
            await RecordAsync(action, "Warning", "Version was not found.", userId, packageName, version, httpContext);
            return new(
                StatusCodes.Status404NotFound,
                new PackageVersionLifecycleResponse(false, "Version was not found.", null));
        }

        if (entity.IsYanked == yanked)
        {
            var conflictMessage = yanked ? "Version is already yanked." : "Version is not yanked.";
            await RecordAsync(action, "Warning", conflictMessage, userId, packageName, version, httpContext);
            return new(
                StatusCodes.Status409Conflict,
                new PackageVersionLifecycleResponse(
                    false,
                    conflictMessage,
                    PackageResponseMapper.ToVersionSummary(entity, package.Name)));
        }

        entity.IsYanked = yanked;
        entity.YankedAtUtc = yanked ? DateTimeOffset.UtcNow : null;
        package.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        var successMessage = yanked ? "Version yanked." : "Version unyanked.";
        logger.LogInformation("{Action} {PackageName} {Version} by {UserId}", action, packageName, version, userId);
        await RecordAsync(action, "Information", successMessage, userId, packageName, version, httpContext);

        return new(
            StatusCodes.Status200OK,
            new PackageVersionLifecycleResponse(
                true,
                successMessage,
                PackageResponseMapper.ToVersionSummary(entity, package.Name)));
    }

    private Task RecordAsync(
        string action,
        string severity,
        string message,
        string? userId,
        string? packageName,
        string? version,
        HttpContext httpContext,
        CancellationToken cancellationToken = default)
        => registryActivity.AppendAsync(new RegistryActivityEntry(
            DateTimeOffset.UtcNow,
            severity,
            action,
            message,
            httpContext.TraceIdentifier,
            userId,
            packageName,
            version), cancellationToken);
}

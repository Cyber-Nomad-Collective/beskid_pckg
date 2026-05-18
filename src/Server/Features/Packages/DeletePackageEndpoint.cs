using System.Collections.Generic;
using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using Server.Data;
using Server.Services;

namespace Server.Features.Packages;

public sealed class DeletePackageEndpoint(
    ApplicationDbContext dbContext,
    IApiPrincipalResolver principalResolver,
    IPackageArtifactStore artifactStore,
    IPckgRegistryActivityLog registryActivity,
    ILogger<DeletePackageEndpoint> logger)
    : EndpointWithoutRequest<DeletePackageResponse>
{
    private const string PackageBoardEntityType = "Package";

    public override void Configure()
    {
        Delete("/packages/{PackageName}");
        Options(x => x.RequireAuthorization());
        Summary(s => s.Summary = "Permanently delete a package, its versions, and stored artifacts.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        using var logScope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["TraceId"] = HttpContext.TraceIdentifier,
        });

        void Record(string severity, string action, string message, string? uid, string? pkg)
        {
            registryActivity.Append(new RegistryActivityEntry(
                DateTimeOffset.UtcNow,
                severity,
                action,
                message,
                HttpContext.TraceIdentifier,
                uid,
                pkg,
                null));
        }

        var userId = await principalResolver.ResolveUserIdAsync(HttpContext, ct);
        if (string.IsNullOrWhiteSpace(userId))
        {
            logger.LogWarning("Delete package rejected: unauthenticated.");
            Record("Warning", "delete_package", "Unauthorized.", null, null);
            await Send.ResponseAsync(new DeletePackageResponse(false, "Unauthorized."), StatusCodes.Status401Unauthorized, ct);
            return;
        }

        var packageName = Route<string>("PackageName")?.Trim();
        if (string.IsNullOrWhiteSpace(packageName))
        {
            Record("Warning", "delete_package", "Package name is required.", userId, null);
            await Send.ResponseAsync(new DeletePackageResponse(false, "Package name is required."), StatusCodes.Status400BadRequest, ct);
            return;
        }

        var package = await dbContext.Packages.SingleOrDefaultAsync(x => x.Name == packageName, ct);
        if (package is null)
        {
            logger.LogWarning("Delete package rejected: {PackageName} not found.", packageName);
            Record("Warning", "delete_package", "Package was not found.", userId, packageName);
            await Send.ResponseAsync(new DeletePackageResponse(false, "Package was not found."), StatusCodes.Status404NotFound, ct);
            return;
        }

        if (package.OwnerUserId != userId && !User.IsInRole("SuperAdmin"))
        {
            logger.LogWarning("Delete package rejected: forbidden for user {UserId} on {PackageName}.", userId, packageName);
            Record("Warning", "delete_package", "You do not have permission to delete this package.", userId, packageName);
            await Send.ResponseAsync(new DeletePackageResponse(false, "You do not have permission to delete this package."), StatusCodes.Status403Forbidden, ct);
            return;
        }

        var packageIdStr = package.Id.ToString();

        var artifactKeys = await dbContext.PackageVersions
            .AsNoTracking()
            .Where(v => v.PackageId == package.Id)
            .Select(v => v.StorageKey)
            .ToListAsync(ct);

        var follows = await dbContext.PackageFollows
            .Where(f => f.PackageId == packageIdStr)
            .ToListAsync(ct);
        if (follows.Count > 0)
        {
            dbContext.PackageFollows.RemoveRange(follows);
        }

        var tags = await dbContext.PackageTags.Where(t => t.PackageId == package.Id).ToListAsync(ct);
        if (tags.Count > 0)
        {
            dbContext.PackageTags.RemoveRange(tags);
        }

        var permissions = await dbContext.ResourcePermissions
            .Where(p => p.ResourceType == "Package" && p.ResourceId == packageIdStr)
            .ToListAsync(ct);
        if (permissions.Count > 0)
        {
            dbContext.ResourcePermissions.RemoveRange(permissions);
        }

        var board = await dbContext.Boards
            .SingleOrDefaultAsync(b => b.EntityType == PackageBoardEntityType && b.EntityId == packageIdStr, ct);
        if (board is not null)
        {
            var topic = await dbContext.Topics.SingleOrDefaultAsync(t => t.BoardId == board.Id, ct);
            if (topic is not null)
            {
                dbContext.Topics.Remove(topic);
            }

            dbContext.Boards.Remove(board);
        }

        dbContext.Packages.Remove(package);

        await dbContext.SaveChangesAsync(ct);

        foreach (var key in artifactKeys)
        {
            await artifactStore.DeleteArtifactAsync(key, ct);
        }

        logger.LogInformation("Deleted package {PackageName} by {UserId}; trace={TraceId}.", packageName, userId, HttpContext.TraceIdentifier);
        Record("Information", "delete_package_success", "Package deleted.", userId, packageName);

        await Send.OkAsync(new DeletePackageResponse(true, "Package deleted."), ct);
    }
}

using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using Server.Data;
using Server.Services;

namespace Server.Features.Packages;

public sealed class UnyankPackageVersionEndpoint(
    ApplicationDbContext dbContext,
    IApiPrincipalResolver principalResolver)
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
        var userId = await principalResolver.ResolveUserIdAsync(HttpContext, ct);
        if (string.IsNullOrWhiteSpace(userId))
        {
            HttpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await Send.OkAsync(new PackageVersionLifecycleResponse(false, "Unauthorized.", null), ct);
            return;
        }

        var packageName = Route<string>("PackageName")?.Trim();
        var version = Route<string>("Version")?.Trim();
        if (string.IsNullOrWhiteSpace(packageName) || string.IsNullOrWhiteSpace(version))
        {
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await Send.OkAsync(new PackageVersionLifecycleResponse(false, "Package name and version are required.", null), ct);
            return;
        }

        var package = await dbContext.Packages.SingleOrDefaultAsync(x => x.Name == packageName, ct);
        if (package is null)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status404NotFound;
            await Send.OkAsync(new PackageVersionLifecycleResponse(false, "Package was not found.", null), ct);
            return;
        }

        if (package.OwnerUserId != userId && !User.IsInRole("SuperAdmin"))
        {
            HttpContext.Response.StatusCode = StatusCodes.Status403Forbidden;
            await Send.OkAsync(new PackageVersionLifecycleResponse(false, "You do not have permission to unyank this version.", null), ct);
            return;
        }

        var entity = await dbContext.PackageVersions
            .SingleOrDefaultAsync(x => x.PackageId == package.Id && x.Version == version, ct);
        if (entity is null)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status404NotFound;
            await Send.OkAsync(new PackageVersionLifecycleResponse(false, "Version was not found.", null), ct);
            return;
        }

        if (!entity.IsYanked)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status409Conflict;
            await Send.OkAsync(new PackageVersionLifecycleResponse(false, "Version is already active.", ToResponse(entity, package.Name)), ct);
            return;
        }

        entity.IsYanked = false;
        entity.YankedAtUtc = null;
        package.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(ct);

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

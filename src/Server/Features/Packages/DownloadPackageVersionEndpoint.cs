using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using Server.Data;
using Server.Services;

namespace Server.Features.Packages;

public sealed class DownloadPackageVersionEndpoint(
    ApplicationDbContext dbContext,
    IApiPrincipalResolver principalResolver,
    IPackageArtifactStore artifactStore)
    : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("/packages/{PackageName}/versions/{Version}/download");
        AllowAnonymous();
        Summary(s => s.Summary = "Download package artifact by version.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var packageName = Route<string>("PackageName")?.Trim();
        var version = Route<string>("Version")?.Trim();
        if (string.IsNullOrWhiteSpace(packageName) || string.IsNullOrWhiteSpace(version))
        {
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        var package = await dbContext.Packages.SingleOrDefaultAsync(x => x.Name == packageName, ct);
        if (package is null)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        if (!package.IsPublic)
        {
            var userId = await principalResolver.ResolveUserIdAsync(HttpContext, ct);
            if (string.IsNullOrWhiteSpace(userId) || userId != package.OwnerUserId)
            {
                HttpContext.Response.StatusCode = StatusCodes.Status403Forbidden;
                return;
            }
        }

        var packageVersion = await dbContext.PackageVersions
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.PackageId == package.Id && x.Version == version && !x.IsYanked, ct);

        if (packageVersion is null)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        var digestOk = await artifactStore.VerifyChecksumAsync(packageVersion.StorageKey, packageVersion.ChecksumSha256, ct);
        if (!digestOk)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
            return;
        }

        var artifact = await artifactStore.OpenReadAsync(packageVersion.StorageKey, ct);
        if (artifact is null)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        package.TotalDownloads += 1;
        package.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(ct);

        HttpContext.Response.ContentType = artifact.Value.ContentType;
        HttpContext.Response.Headers.ContentDisposition = $"attachment; filename=\"{package.Name}-{packageVersion.Version}.bpk\"";
        if (artifact.Value.SizeBytes is long size)
        {
            HttpContext.Response.ContentLength = size;
        }

        await using var stream = artifact.Value.Stream;
        await stream.CopyToAsync(HttpContext.Response.Body, ct);
    }
}

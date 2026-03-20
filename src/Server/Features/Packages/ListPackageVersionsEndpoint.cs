using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using Server.Data;
using Server.Services;

namespace Server.Features.Packages;

public sealed class ListPackageVersionsEndpoint(
    ApplicationDbContext dbContext,
    IApiPrincipalResolver principalResolver)
    : EndpointWithoutRequest<List<PackageVersionSummaryResponse>>
{
    public override void Configure()
    {
        Get("/packages/{PackageName}/versions");
        AllowAnonymous();
        Summary(s => s.Summary = "List published versions for a package.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var packageName = Route<string>("PackageName")?.Trim();
        if (string.IsNullOrWhiteSpace(packageName))
        {
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await Send.OkAsync([], ct);
            return;
        }

        var package = await dbContext.Packages.AsNoTracking().SingleOrDefaultAsync(x => x.Name == packageName, ct);
        if (package is null)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status404NotFound;
            await Send.OkAsync([], ct);
            return;
        }

        if (!package.IsPublic)
        {
            var userId = await principalResolver.ResolveUserIdAsync(HttpContext, ct);
            if (string.IsNullOrWhiteSpace(userId) || userId != package.OwnerUserId)
            {
                HttpContext.Response.StatusCode = StatusCodes.Status403Forbidden;
                await Send.OkAsync([], ct);
                return;
            }
        }

        var versionEntities = await dbContext.PackageVersions
            .AsNoTracking()
            .Where(x => x.PackageId == package.Id)
            .ToListAsync(ct);

        var versions = versionEntities
            .OrderByDescending(x => x.PublishedAtUtc)
            .Select(x => new PackageVersionSummaryResponse(
                x.Id,
                x.PackageId,
                package.Name,
                x.Version,
                x.IsYanked,
                x.ChecksumSha256,
                x.SizeBytes,
                x.PublishedAtUtc,
                x.YankedAtUtc))
            .ToList();

        await Send.OkAsync(versions, ct);
    }
}

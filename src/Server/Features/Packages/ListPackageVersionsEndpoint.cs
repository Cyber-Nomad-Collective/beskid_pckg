using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using Server.Data;
using Server.Features.Packages.Mapping;
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
            await Send.ResponseAsync([], StatusCodes.Status400BadRequest, ct);
            return;
        }

        var package = await dbContext.Packages.AsNoTracking().SingleOrDefaultAsync(x => x.Name == packageName, ct);
        if (package is null)
        {
            await Send.ResponseAsync([], StatusCodes.Status404NotFound, ct);
            return;
        }

        if (!package.IsPublic)
        {
            var userId = await principalResolver.ResolveUserIdAsync(HttpContext, ct);
            if (string.IsNullOrWhiteSpace(userId) || userId != package.OwnerUserId)
            {
                await Send.ResponseAsync([], StatusCodes.Status403Forbidden, ct);
                return;
            }
        }

        var versionEntities = await dbContext.PackageVersions
            .AsNoTracking()
            .Where(x => x.PackageId == package.Id)
            .ToListAsync(ct);

        var versions = versionEntities
            .OrderByDescending(x => x.PublishedAtUtc)
            .Select(x => PackageResponseMapper.ToVersionSummary(x, package.Name))
            .ToList();

        await Send.OkAsync(versions, ct);
    }
}

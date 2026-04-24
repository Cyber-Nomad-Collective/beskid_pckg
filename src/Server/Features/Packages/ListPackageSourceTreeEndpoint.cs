using FastEndpoints;
using Server.Services;

namespace Server.Features.Packages;

public sealed class ListPackageSourceTreeEndpoint(IPackageSourceArchiveService sourceArchive)
    : EndpointWithoutRequest<PackageSourceTreeResponse>
{
    public override void Configure()
    {
        Get("/packages/{IdOrName}/versions/{Version}/source/tree");
        Options(x => x.RequireRateLimiting("docs"));
        AllowAnonymous();
        Summary(s => s.Summary = "List source file hierarchy for a package version.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var idOrName = Route<string>("IdOrName")?.Trim() ?? string.Empty;
        var version = Route<string>("Version")?.Trim() ?? string.Empty;

        var result = await sourceArchive.ListTreeAsync(HttpContext, idOrName, version, ct);
        if (result.StatusCode != StatusCodes.Status200OK)
        {
            if (result.StatusCode == StatusCodes.Status404NotFound)
            {
                await Send.NotFoundAsync(ct);
                return;
            }

            await Send.StringAsync(string.Empty, result.StatusCode, cancellation: ct);
            return;
        }

        await Send.OkAsync(new PackageSourceTreeResponse(result.Nodes ?? []), ct);
    }
}

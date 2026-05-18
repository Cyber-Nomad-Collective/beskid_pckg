using FastEndpoints;
using Server.Features.Packages.Internal;
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
        if (await PackageArtifactEndpointResults.TrySendErrorAsync(this, result.StatusCode, ct))
        {
            return;
        }

        await Send.OkAsync(new PackageSourceTreeResponse(result.Nodes ?? []), ct);
    }
}

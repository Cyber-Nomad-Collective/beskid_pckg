using FastEndpoints;
using Server.Features.Packages.Internal;
using Server.Services;

namespace Server.Features.Packages;

public sealed class ListPackageDocsEndpoint(IPackageDocsArchiveService docsArchive)
    : EndpointWithoutRequest<PackageDocsIndexResponse>
{
    public override void Configure()
    {
        Get("/packages/{IdOrName}/versions/{Version}/docs");
        Options(x => x.RequireRateLimiting("docs"));
        AllowAnonymous();
        Summary(s => s.Summary = "List markdown documentation files for a package version, plus flags when structured api.json is present.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var idOrName = Route<string>("IdOrName")?.Trim() ?? string.Empty;
        var version = Route<string>("Version")?.Trim() ?? string.Empty;

        var result = await docsArchive.ListDocsAsync(HttpContext, idOrName, version, ct);
        if (await PackageArtifactEndpointResults.TrySendErrorAsync(this, result.StatusCode, ct))
        {
            return;
        }

        await Send.OkAsync(
            new PackageDocsIndexResponse(
                result.Files ?? [],
                result.HasStructuredApiDoc,
                result.StructuredDocRelativePath),
            ct);
    }
}

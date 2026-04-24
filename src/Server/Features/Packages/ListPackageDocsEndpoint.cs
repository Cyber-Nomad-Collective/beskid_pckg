using FastEndpoints;
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

        await Send.OkAsync(
            new PackageDocsIndexResponse(
                result.Files ?? [],
                result.HasStructuredApiDoc,
                result.StructuredDocRelativePath),
            ct);
    }
}

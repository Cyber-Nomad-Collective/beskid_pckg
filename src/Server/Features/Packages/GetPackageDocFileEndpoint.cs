using FastEndpoints;
using Server.Features.Packages.Internal;
using Server.Services;

namespace Server.Features.Packages;

public sealed class GetPackageDocFileEndpoint(IPackageDocsArchiveService docsArchive) : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("/packages/{IdOrName}/versions/{Version}/docs/file");
        Options(x => x.RequireRateLimiting("docs"));
        AllowAnonymous();
        Summary(s => s.Summary = "Get raw markdown for a documentation file in the package artifact.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var idOrName = Route<string>("IdOrName")?.Trim() ?? string.Empty;
        var version = Route<string>("Version")?.Trim() ?? string.Empty;
        var path = Query<string>("path", isRequired: false);

        var result = await docsArchive.ReadDocAsync(HttpContext, idOrName, version, path ?? string.Empty, ct);
        if (await PackageArtifactEndpointResults.TrySendErrorAsync(this, result.StatusCode, ct))
        {
            return;
        }

        await Send.StringAsync(
            result.Markdown ?? string.Empty,
            StatusCodes.Status200OK,
            result.ContentType ?? "text/markdown",
            ct);
    }
}

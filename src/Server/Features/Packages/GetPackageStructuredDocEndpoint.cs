using FastEndpoints;
using Server.Features.Packages.Internal;
using Server.Services;

namespace Server.Features.Packages;

public sealed class GetPackageStructuredDocEndpoint(IPackageDocsArchiveService docsArchive) : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("/packages/{IdOrName}/versions/{Version}/docs/structured");
        Options(x => x.RequireRateLimiting("docs"));
        AllowAnonymous();
        Summary(s => s.Summary = "Get structured API documentation (Beskid api.json) for a package version when present.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var idOrName = Route<string>("IdOrName")?.Trim() ?? string.Empty;
        var version = Route<string>("Version")?.Trim() ?? string.Empty;

        var result = await docsArchive.ReadStructuredDocAsync(HttpContext, idOrName, version, ct);
        if (await PackageArtifactEndpointResults.TrySendErrorAsync(this, result.StatusCode, ct))
        {
            return;
        }

        await Send.StringAsync(
            result.Json ?? string.Empty,
            StatusCodes.Status200OK,
            result.ContentType ?? "application/json",
            ct);
    }
}

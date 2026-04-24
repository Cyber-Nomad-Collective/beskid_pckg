using FastEndpoints;
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

        HttpContext.Response.ContentType = result.ContentType;
        await HttpContext.Response.WriteAsync(result.Json ?? string.Empty, ct);
    }
}

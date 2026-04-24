using FastEndpoints;
using Server.Services;

namespace Server.Features.Packages;

public sealed class GetPackageSourceFileEndpoint(IPackageSourceArchiveService sourceArchive)
    : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("/packages/{IdOrName}/versions/{Version}/source/file");
        Options(x => x.RequireRateLimiting("docs"));
        AllowAnonymous();
        Summary(s => s.Summary = "Get source file payload for a package version.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var idOrName = Route<string>("IdOrName")?.Trim() ?? string.Empty;
        var version = Route<string>("Version")?.Trim() ?? string.Empty;
        var path = Query<string>("path", isRequired: false);

        var result = await sourceArchive.ReadFileAsync(HttpContext, idOrName, version, path ?? string.Empty, ct);
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

        HttpContext.Response.ContentType = result.ContentType ?? "application/octet-stream";
        HttpContext.Response.Headers["X-Beskid-Source-Preview"] = result.PreviewKind.ToString().ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(result.MonacoLanguage))
        {
            HttpContext.Response.Headers["X-Beskid-Monaco-Language"] = result.MonacoLanguage!;
        }

        if (!string.IsNullOrWhiteSpace(result.FileTypeKind))
        {
            HttpContext.Response.Headers["X-Beskid-File-Type"] = result.FileTypeKind!;
        }

        if (result.Text is not null)
        {
            await HttpContext.Response.WriteAsync(result.Text, ct);
            return;
        }

        if (result.Bytes is { Length: > 0 })
        {
            await HttpContext.Response.Body.WriteAsync(result.Bytes, ct);
            return;
        }

        await Send.StringAsync(string.Empty, StatusCodes.Status200OK, cancellation: ct);
    }
}

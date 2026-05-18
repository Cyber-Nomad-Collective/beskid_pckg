using FastEndpoints;
using Server.Features.Packages.Internal;
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
        if (await PackageArtifactEndpointResults.TrySendErrorAsync(this, result.StatusCode, ct))
        {
            return;
        }

        HttpContext.Response.Headers["X-Beskid-Source-Preview"] = result.PreviewKind.ToString().ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(result.MonacoLanguage))
        {
            HttpContext.Response.Headers["X-Beskid-Monaco-Language"] = result.MonacoLanguage!;
        }

        if (!string.IsNullOrWhiteSpace(result.FileTypeKind))
        {
            HttpContext.Response.Headers["X-Beskid-File-Type"] = result.FileTypeKind!;
        }

        var contentType = result.ContentType ?? "application/octet-stream";
        if (result.Text is not null)
        {
            await Send.StringAsync(result.Text, StatusCodes.Status200OK, contentType, ct);
            return;
        }

        if (result.Bytes is { Length: > 0 })
        {
            await Send.BytesAsync(result.Bytes, string.Empty, contentType, cancellation: ct);
            return;
        }

        await Send.StringAsync(string.Empty, StatusCodes.Status200OK, contentType, ct);
    }
}

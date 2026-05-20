using FastEndpoints;
using Server.Features.Packages.Internal;
using Server.Services;

namespace Server.Features.Packages;

public sealed class GetPackageReadmeEndpoint(
    IPackageDocsArchiveService docsArchive,
    IPackageArtifactExplorerService artifactExplorer) : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("/packages/{IdOrName}/versions/{Version}/readme");
        Options(x => x.RequireRateLimiting("docs"));
        AllowAnonymous();
        Summary(s => s.Summary = "Get README markdown for a package version (persisted at publish, else artifact fallback).");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var idOrName = Route<string>("IdOrName")?.Trim() ?? string.Empty;
        var version = Route<string>("Version")?.Trim() ?? string.Empty;
        var manifestPath = Query<string>("manifestReadme", isRequired: false);

        var resolved = await artifactExplorer.ResolveVersionAsync(HttpContext, idOrName, version, ct);
        if (!resolved.IsSuccess || resolved.Version is null)
        {
            await PackageArtifactEndpointResults.TrySendErrorAsync(this, resolved.StatusCode, ct);
            return;
        }

        if (!string.IsNullOrWhiteSpace(resolved.Version.ReadmeMarkdown))
        {
            await Send.StringAsync(
                resolved.Version.ReadmeMarkdown,
                StatusCodes.Status200OK,
                "text/markdown; charset=utf-8",
                ct);
            return;
        }

        var result = await docsArchive.ReadReadmeAsync(
            HttpContext,
            idOrName,
            version,
            manifestPath,
            ct);
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

using FastEndpoints;
using Server.Data;

namespace Server.Features.Packages;

/// <summary>Shields-style SVG for GitHub README and other markdown hosts (anonymous, public packages only).</summary>
public sealed class GetPackageEmbedBadgeEndpoint(ApplicationDbContext dbContext) : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("/embed/badge.svg");
        AllowAnonymous();
        Options(o => o.RequireRateLimiting(PackageEmbedUrls.EmbedRateLimitPolicyName));
        Summary(s => s.Summary = "SVG badge for embedding (GitHub README, docs). Public packages only. Pass package via query string.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var routeName = HttpContext.Request.Query[PackageEmbedUrls.PackageQueryKey].FirstOrDefault()?.Trim() ?? string.Empty;
        if (routeName.Length == 0)
        {
            await WriteBadgeAsync(PackageEmbedBadgeSvg.NotFoundBadge(), ct);
            return;
        }

        var dto = await PackageEmbedQueries.TryGetPublicAsync(dbContext, routeName, ct);
        var bytes = dto is null ? PackageEmbedBadgeSvg.NotFoundBadge() : PackageEmbedBadgeSvg.Build(dto);
        await WriteBadgeAsync(bytes, ct);
    }

    private async Task WriteBadgeAsync(byte[] bytes, CancellationToken ct)
    {
        HttpContext.Response.Headers.CacheControl = "public, max-age=120";
        await Send.BytesAsync(bytes, string.Empty, "image/svg+xml; charset=utf-8", cancellation: ct);
    }
}

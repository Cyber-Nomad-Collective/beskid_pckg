using FastEndpoints;
using Server.Data;

namespace Server.Features.Packages;

/// <summary>Minimal HTML card for iframe embedding on external sites.</summary>
public sealed class GetPackageEmbedCardEndpoint(ApplicationDbContext dbContext) : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("/embed/card");
        AllowAnonymous();
        Options(o => o.RequireRateLimiting(PackageEmbedUrls.EmbedRateLimitPolicyName));
        Summary(s => s.Summary = "HTML widget for iframe embedding. Public packages only. Pass package via query string.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var routeName = HttpContext.Request.Query[PackageEmbedUrls.PackageQueryKey].FirstOrDefault()?.Trim() ?? string.Empty;
        if (routeName.Length == 0)
        {
            await WriteNotFoundAsync(ct);
            return;
        }

        var dto = await PackageEmbedQueries.TryGetPublicAsync(dbContext, routeName, ct);
        if (dto is null)
        {
            await WriteNotFoundAsync(ct);
            return;
        }

        HttpContext.Response.Headers.ContentSecurityPolicy = "frame-ancestors *";
        HttpContext.Response.Headers.CacheControl = "public, max-age=120";
        var bytes = PackageEmbedCardHtml.Build(HttpContext.Request, dto);
        await Send.BytesAsync(bytes, string.Empty, "text/html; charset=utf-8", cancellation: ct);
    }

    private Task WriteNotFoundAsync(CancellationToken ct) =>
        Send.StringAsync(
            "<!DOCTYPE html><html><body><p>Package not found.</p></body></html>",
            StatusCodes.Status404NotFound,
            "text/html; charset=utf-8",
            ct);
}

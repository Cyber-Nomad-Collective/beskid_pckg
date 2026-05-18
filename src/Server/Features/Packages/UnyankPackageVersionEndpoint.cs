using FastEndpoints;

namespace Server.Features.Packages;

public sealed class UnyankPackageVersionEndpoint(IPackageVersionLifecycleService lifecycle)
    : EndpointWithoutRequest<PackageVersionLifecycleResponse>
{
    public override void Configure()
    {
        Post("/packages/{PackageName}/versions/{Version}/unyank");
        Options(x => x.RequireAuthorization());
        Summary(s => s.Summary = "Restore a yanked package version.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var packageName = Route<string>("PackageName")?.Trim() ?? string.Empty;
        var version = Route<string>("Version")?.Trim() ?? string.Empty;
        var result = await lifecycle.SetYankedAsync(HttpContext, packageName, version, yanked: false, ct);
        await Send.ResponseAsync(result.Body, result.StatusCode, ct);
    }
}

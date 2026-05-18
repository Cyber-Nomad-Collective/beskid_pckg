using FastEndpoints;

namespace Server.Features.Packages;

public sealed class YankPackageVersionEndpoint(IPackageVersionLifecycleService lifecycle)
    : EndpointWithoutRequest<PackageVersionLifecycleResponse>
{
    public override void Configure()
    {
        Post("/packages/{PackageName}/versions/{Version}/yank");
        Options(x => x.RequireAuthorization());
        Summary(s => s.Summary = "Mark a package version as yanked.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var packageName = Route<string>("PackageName")?.Trim() ?? string.Empty;
        var version = Route<string>("Version")?.Trim() ?? string.Empty;
        var result = await lifecycle.SetYankedAsync(HttpContext, packageName, version, yanked: true, ct);
        await Send.ResponseAsync(result.Body, result.StatusCode, ct);
    }
}

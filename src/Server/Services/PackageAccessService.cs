using Server.Data;

namespace Server.Services;

public interface IPackageAccessService
{
    Task<bool> CanViewPackageAsync(HttpContext httpContext, PackageEntity package, CancellationToken cancellationToken = default);
}

public sealed class PackageAccessService(IApiPrincipalResolver principalResolver) : IPackageAccessService
{
    public async Task<bool> CanViewPackageAsync(
        HttpContext httpContext,
        PackageEntity package,
        CancellationToken cancellationToken = default)
    {
        if (package.IsPublic)
        {
            return true;
        }

        var userId = await principalResolver.ResolveUserIdAsync(httpContext, cancellationToken);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return false;
        }

        if (httpContext.User.IsInRole("SuperAdmin"))
        {
            return true;
        }

        return string.Equals(userId, package.OwnerUserId, StringComparison.Ordinal);
    }
}

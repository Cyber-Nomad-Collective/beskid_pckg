using Server.Features.Packages;
using Server.Services;
using Server.Services.Artifacts;

namespace Server.DependencyInjection;

public static class PackageRegistryServiceCollectionExtensions
{
    public static IServiceCollection AddPackageRegistryServices(this IServiceCollection services)
    {
        services.AddSingleton<IPackageArtifactStore, PackageArtifactStore>();
        services.AddSingleton<IPackageArtifactValidator, PackageArtifactValidator>();
        services.AddScoped<IPackageAccessService, PackageAccessService>();
        services.AddScoped<IPackageVersionLifecycleService, PackageVersionLifecycleService>();
        services.AddScoped<IPackageArtifactExplorerService, PackageArtifactExplorerService>();
        services.AddScoped<IPackageArtifactZipReader, PackageArtifactZipReader>();
        services.AddScoped<IPackageDocsArchiveService, PackageDocsArchiveService>();
        services.AddScoped<IPackageSourceFileTypeMapper, PackageSourceFileTypeMapper>();
        services.AddScoped<IPackageSourceArchiveService, PackageSourceArchiveService>();
        services.AddScoped<IPackageDetailsQuery, PackageDetailsQuery>();
        return services;
    }
}

using Microsoft.Extensions.Configuration;
using Server.Features.Packages;
using Server.Services;
using Server.Services.Artifacts;
using Server.Services.Workspace;

namespace Server.DependencyInjection;

public static class PackageRegistryServiceCollectionExtensions
{
    public static IServiceCollection AddPackageRegistryServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<PackagePublishOptions>(configuration.GetSection(PackagePublishOptions.SectionName));
        services.AddSingleton<IPackageArtifactStore, PackageArtifactStore>();
        services.AddSingleton<IPackageArtifactValidator, PackageArtifactValidator>();
        services.AddScoped<IPackagePublishService, PackagePublishService>();
        services.AddScoped<IWorkspacePublishService, WorkspacePublishService>();
        services.AddSingleton<IPackageArtifactPublishMetadataExtractor, PackageArtifactPublishMetadataExtractor>();
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

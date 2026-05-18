using Server.Services;

namespace Server.DependencyInjection;

public static class DocumentationServiceCollectionExtensions
{
    public static IServiceCollection AddDocumentationServices(this IServiceCollection services)
    {
        services.AddSingleton<IMarkdownService, MarkdownService>();
        services.AddSingleton<IHtmlSanitizationService, HtmlSanitizationService>();
        services.AddScoped<IDocumentationRendering, DocumentationRendering>();
        services.AddScoped<IPackageArtifactClient, PackageArtifactClient>();
        return services;
    }
}

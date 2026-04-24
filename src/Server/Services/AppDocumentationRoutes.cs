namespace Server.Services;

/// <summary>
/// Browser paths for package documentation (Blazor app routes, not <c>/api</c>).
/// Use <see cref="Uri.EscapeDataString"/> for qualified names and symbols that contain reserved characters (e.g. <c>::</c>).
/// </summary>
public static class AppDocumentationRoutes
{
    public static string AppDocsBase(string packageId, string version) =>
        $"/docs/{Uri.EscapeDataString($"{packageId}@{version}")}";

    public static string AppDocsApiMember(string packageId, string version, string qualifiedName) =>
        $"{AppDocsBase(packageId, version)}/api/{Uri.EscapeDataString(qualifiedName)}";

    public static string AppDocsSymbolSearch(string packageId, string version, string symbol) =>
        $"{AppDocsBase(packageId, version)}/search/{Uri.EscapeDataString(symbol)}";
}

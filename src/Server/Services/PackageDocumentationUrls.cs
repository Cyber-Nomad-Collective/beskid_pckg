namespace Server.Services;

/// <summary>
/// Central URL builders for package documentation HTTP APIs (Blazor <c>HttpClient</c> base address is <c>/api</c>).
/// </summary>
public static class PackageDocumentationUrls
{
    public static string DocsIndex(string packageId, string version) =>
        $"/api/packages/{Uri.EscapeDataString(packageId)}/versions/{Uri.EscapeDataString(version)}/docs";

    public static string DocsFile(string packageId, string version, string path) =>
        $"/api/packages/{Uri.EscapeDataString(packageId)}/versions/{Uri.EscapeDataString(version)}/docs/file?path={Uri.EscapeDataString(path)}";

    public static string DocsStructured(string packageId, string version) =>
        $"/api/packages/{Uri.EscapeDataString(packageId)}/versions/{Uri.EscapeDataString(version)}/docs/structured";

    public static string SourceTree(string packageId, string version) =>
        $"/api/packages/{Uri.EscapeDataString(packageId)}/versions/{Uri.EscapeDataString(version)}/source/tree";

    public static string SourceFile(string packageId, string version, string path) =>
        $"/api/packages/{Uri.EscapeDataString(packageId)}/versions/{Uri.EscapeDataString(version)}/source/file?path={Uri.EscapeDataString(path)}";
}

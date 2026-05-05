namespace Server.Features.Packages;

/// <summary>Builds registry-relative URLs for README badges and iframe widgets (GitHub and other hosts).</summary>
public static class PackageEmbedUrls
{
    public const string EmbedRateLimitPolicyName = "embed";

    public const string PackageQueryKey = "package";

    public static string BadgeRelativePath(string packageName)
        => "/api/embed/badge.svg?" + PackageQueryKey + "=" + Uri.EscapeDataString(packageName.Trim());

    public static string CardRelativePath(string packageName)
        => "/api/embed/card?" + PackageQueryKey + "=" + Uri.EscapeDataString(packageName.Trim());

    /// <summary>Absolute badge URL for markdown / HTML (uses request host and scheme).</summary>
    public static string BadgeAbsoluteUrl(HttpRequest request, string packageName)
        => $"{request.Scheme}://{request.Host}{BadgeRelativePath(packageName)}";

    public static string CardAbsoluteUrl(HttpRequest request, string packageName)
        => $"{request.Scheme}://{request.Host}{CardRelativePath(packageName)}";
}

namespace Server.Services;

/// <summary>
/// <c>packageKind</c> values on <c>beskid.package.v1</c> (see platform-spec tooling/registry-client/package-kinds).
/// </summary>
public static class PackageKinds
{
    public const string Library = "library";
    public const string Template = "template";
    public const string Tool = "tool";

    public static string NormalizeOrDefault(string? packageKind)
        => string.IsNullOrWhiteSpace(packageKind) ? Library : packageKind.Trim();

    public static bool IsSupported(string? packageKind)
    {
        var normalized = NormalizeOrDefault(packageKind);
        return string.Equals(normalized, Library, StringComparison.OrdinalIgnoreCase)
               || string.Equals(normalized, Template, StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsTemplate(string? packageKind)
        => string.Equals(NormalizeOrDefault(packageKind), Template, StringComparison.OrdinalIgnoreCase);
}

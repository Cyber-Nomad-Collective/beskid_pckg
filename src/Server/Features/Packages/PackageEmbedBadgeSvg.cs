using System.Net;
using System.Text;

namespace Server.Features.Packages;

internal static class PackageEmbedBadgeSvg
{
    private const int Height = 20;
    private const double FontSize = 11;
    private const double CharWidth = 6.2;
    private const int HorizontalPadding = 10;

    public static byte[] NotFoundBadge()
        => BuildTwoPart("not found", "#9a9a9a");

    public static byte[] Build(PackageEmbedPublicDto dto)
        => BuildTwoPart(BuildRightText(dto), "#007ec6");

    private static byte[] BuildTwoPart(string rightText, string rightFill)
    {
        const string leftLabel = "pckg";
        var leftWidth = (int)Math.Ceiling(leftLabel.Length * CharWidth + HorizontalPadding * 2);
        var rightWidth = (int)Math.Ceiling(rightText.Length * CharWidth + HorizontalPadding * 2);
        var totalWidth = leftWidth + rightWidth;

        var sb = new StringBuilder(512);
        sb.Append($"""
            <svg xmlns="http://www.w3.org/2000/svg" width="{totalWidth}" height="{Height}" role="img" aria-label="{EscapeXml($"{leftLabel} {rightText}")}">
              <title>{EscapeXml($"{leftLabel} {rightText}")}</title>
              <linearGradient id="a" x2="0" y2="100%">
                <stop offset="0" stop-color="#bbb" stop-opacity=".1"/>
                <stop offset="1" stop-opacity=".1"/>
              </linearGradient>
              <rect rx="3" width="{totalWidth}" height="{Height}" fill="#555"/>
              <rect rx="3" x="{leftWidth}" width="{rightWidth}" height="{Height}" fill="{rightFill}"/>
              <rect rx="3" width="{totalWidth}" height="{Height}" fill="url(#a)"/>
              <g fill="#fff" text-anchor="middle" font-family="DejaVu Sans,Verdana,Geneva,sans-serif" font-size="{FontSize:0.##}">
                <text x="{leftWidth / 2d:0.##}" y="14" fill="#010101" fill-opacity=".3">{EscapeXml(leftLabel)}</text>
                <text x="{leftWidth / 2d:0.##}" y="13">{EscapeXml(leftLabel)}</text>
                <text x="{leftWidth + rightWidth / 2d:0.##}" y="14" fill="#010101" fill-opacity=".3">{EscapeXml(rightText)}</text>
                <text x="{leftWidth + rightWidth / 2d:0.##}" y="13">{EscapeXml(rightText)}</text>
              </g>
            </svg>
            """);

        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    private static string BuildRightText(PackageEmbedPublicDto dto)
    {
        var displayName = dto.Name.Length > 36 ? dto.Name[..33] + "…" : dto.Name;
        var ver = string.IsNullOrWhiteSpace(dto.LatestVersion) ? "no release" : dto.LatestVersion.Trim();
        return $"{displayName} · {ver}";
    }

    internal static string EscapeXml(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return WebUtility.HtmlEncode(value);
    }
}

using System.Globalization;
using System.Net;
using System.Text;

namespace Server.Features.Packages;

internal static class PackageEmbedCardHtml
{
    private const string CssBlock = """
        <style>
          :root { color-scheme: light dark; }
          body { margin: 0; font-family: system-ui, -apple-system, Segoe UI, Roboto, Ubuntu, Cantarell, "Helvetica Neue", Arial, sans-serif; background: #f5f5f5; color: #111; }
          @media (prefers-color-scheme: dark) {
            body { background: #1a1a1a; color: #f3f3f3; }
            a { color: #6cb6ff; }
          }
          .card {
            box-sizing: border-box;
            max-width: 420px;
            margin: 0 auto;
            padding: 12px 14px;
            border-radius: 8px;
            border: 1px solid rgba(127,127,127,.35);
            background: rgba(255,255,255,.92);
            box-shadow: 0 1px 2px rgba(0,0,0,.06);
          }
          @media (prefers-color-scheme: dark) {
            .card { background: rgba(40,40,40,.95); }
          }
          .brand { font-size: 11px; letter-spacing: .04em; text-transform: uppercase; opacity: .75; margin-bottom: 6px; }
          h1 { font-size: 16px; margin: 0 0 6px 0; line-height: 1.25; }
          p.meta { margin: 0 0 8px 0; font-size: 13px; opacity: .9; }
          p.desc { margin: 0 0 10px 0; font-size: 13px; line-height: 1.35; opacity: .92; }
          .row { display: flex; align-items: center; justify-content: space-between; gap: 10px; flex-wrap: wrap; }
          img.badge { height: 20px; display: block; }
          a.pkg { text-decoration: none; color: inherit; }
          a.pkg:hover { text-decoration: underline; }
        </style>
        """;

    public static byte[] Build(HttpRequest request, PackageEmbedPublicDto dto)
    {
        var packageUrl = $"{request.Scheme}://{request.Host}/packages/{Uri.EscapeDataString(dto.Name)}";
        var badgeUrl = PackageEmbedUrls.BadgeAbsoluteUrl(request, dto.Name);
        var nameHtml = WebUtility.HtmlEncode(dto.Name);
        var desc = string.IsNullOrWhiteSpace(dto.Description)
            ? "Beskid package on the registry."
            : dto.Description.Trim();
        if (desc.Length > 160)
        {
            desc = desc[..157] + "…";
        }

        var descHtml = WebUtility.HtmlEncode(desc);
        var versionLine = string.IsNullOrWhiteSpace(dto.LatestVersion)
            ? "Latest: <strong>none published</strong>"
            : $"""Latest: <strong>{WebUtility.HtmlEncode(dto.LatestVersion)}</strong>""";
        var downloads = dto.TotalDownloads.ToString("N0", CultureInfo.InvariantCulture);
        var packageUrlAttr = WebUtility.HtmlEncode(packageUrl);
        var badgeUrlAttr = WebUtility.HtmlEncode(badgeUrl);

        var sb = new StringBuilder(2048);
        sb.Append("""
            <!DOCTYPE html>
            <html lang="en">
            <head>
              <meta charset="utf-8"/>
              <meta name="viewport" content="width=device-width, initial-scale=1"/>
              <meta name="robots" content="noindex"/>
              <base target="_blank" rel="noopener noreferrer"/>
            """);
        sb.Append(CssBlock);
        sb.Append("""
            </head>
            <body>
              <article class="card">
                <div class="brand">Beskid registry</div>
            """);
        sb.Append($"""
                <h1><a class="pkg" href="{packageUrlAttr}">{nameHtml}</a></h1>
                <p class="meta">{versionLine} · {downloads} downloads</p>
                <p class="desc">{descHtml}</p>
                <div class="row">
                  <a href="{badgeUrlAttr}"><img class="badge" src="{badgeUrlAttr}" alt="pckg registry badge"/></a>
                  <a href="{packageUrlAttr}">View package →</a>
                </div>
              </article>
            </body>
            </html>
            """);

        return Encoding.UTF8.GetBytes(sb.ToString());
    }
}

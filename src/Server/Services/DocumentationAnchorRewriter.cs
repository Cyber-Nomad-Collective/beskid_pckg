using System.Text.RegularExpressions;

namespace Server.Services;

/// <summary>
/// Rewrites fragment-only <c>href="#…"</c> attributes so they resolve on the current Blazor route.
/// With <c>&lt;base href="/" /&gt;</c>, bare fragments resolve against the site root (home), not the active page.
/// </summary>
public static class DocumentationAnchorRewriter
{
    /// <summary>
    /// Matches <c>href="#fragment"</c> (single or double quotes).
    /// </summary>
    private static readonly Regex FragmentOnlyHrefRegex = new(
        @"href\s*=\s*(['""])(#[^'""<>]*)\1",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static string RewriteFragmentOnlyAnchors(string html, string currentAbsolutePath)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return html;
        }

        var path = NormalizeAbsolutePath(currentAbsolutePath);

        return FragmentOnlyHrefRegex.Replace(html, m =>
        {
            var quote = m.Groups[1].Value;
            var fragment = m.Groups[2].Value;
            return $"href={quote}{path}{fragment}{quote}";
        });
    }

    private static string NormalizeAbsolutePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "/";
        }

        return path.StartsWith('/') ? path : "/" + path;
    }
}

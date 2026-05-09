using System.Text.RegularExpressions;

namespace Server.Services;

/// <summary>
/// Rewrites link <c>href</c> attributes so intra-package documentation resolves on the active Blazor route.
/// With <c>&lt;base href="/" /&gt;</c>, fragment-only and relative URLs resolve against the site root (home), not the docs page.
/// </summary>
public static class DocumentationAnchorRewriter
{
    private static readonly Uri PlaceholderOrigin = new("https://doc-anchor.invalid/", UriKind.Absolute);

    /// <summary>
    /// Matches <c>href="…"</c> (single or double quotes).
    /// </summary>
    private static readonly Regex HrefAttributeRegex = new(
        @"href\s*=\s*(['""])(?<val>.*?)\1",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Singleline | RegexOptions.Compiled);

    /// <summary>
    /// Rewrites fragment-only, empty, and relative <c>href</c> values so they stay on the current route.
    /// </summary>
    public static string RewriteDocumentationAnchors(string html, string currentAbsolutePath)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return html;
        }

        var path = NormalizeAbsolutePath(currentAbsolutePath);

        return HrefAttributeRegex.Replace(html, m =>
        {
            var quote = m.Groups[1].Value;
            var val = m.Groups["val"].Value;
            var rewritten = RewriteHrefValue(val, path);
            return $"href={quote}{rewritten}{quote}";
        });
    }

    private static string RewriteHrefValue(string rawHref, string path)
    {
        var trimmed = rawHref.Trim();
        if (trimmed.Length == 0)
        {
            return path;
        }

        if (trimmed.StartsWith('#'))
        {
            return $"{path}{trimmed}";
        }

        if (ShouldLeaveHrefUnchanged(trimmed))
        {
            return rawHref;
        }

        try
        {
            var dirBase = CreateDirectoryBaseUri(path);
            var resolved = new Uri(dirBase, trimmed);
            return resolved.AbsolutePath + resolved.Query + resolved.Fragment;
        }
        catch (UriFormatException)
        {
            return rawHref;
        }
    }

    private static bool ShouldLeaveHrefUnchanged(string trimmed)
    {
        if (trimmed.StartsWith("//", StringComparison.Ordinal))
        {
            return true;
        }

        if (trimmed.StartsWith('/'))
        {
            return true;
        }

        if (trimmed.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("tel:", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return HasAbsoluteUriScheme(trimmed);
    }

    private static bool HasAbsoluteUriScheme(string trimmed)
    {
        var colon = trimmed.IndexOf(':');
        if (colon <= 0 || colon > 32)
        {
            return false;
        }

        var scheme = trimmed[..colon];
        return scheme.Equals("http", StringComparison.OrdinalIgnoreCase)
            || scheme.Equals("https", StringComparison.OrdinalIgnoreCase);
    }

    private static Uri CreateDirectoryBaseUri(string absolutePath)
    {
        var dir = GetDirectoryPath(absolutePath);
        return new Uri(PlaceholderOrigin, dir);
    }

    private static string GetDirectoryPath(string absolutePath)
    {
        var path = NormalizeAbsolutePath(absolutePath);
        if (path.Length <= 1)
        {
            return "/";
        }

        var lastSlash = path.LastIndexOf('/');
        if (lastSlash <= 0)
        {
            return "/";
        }

        return path[..(lastSlash + 1)];
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

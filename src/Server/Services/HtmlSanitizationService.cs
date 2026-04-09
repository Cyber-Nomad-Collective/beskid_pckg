using System.Net;
using System.Text.RegularExpressions;
using Ganss.Xss;

namespace Server.Services;

public interface IHtmlSanitizationService
{
    string Sanitize(string? html);
    string ToPlainText(string? html);
}

public sealed class HtmlSanitizationService : IHtmlSanitizationService
{
    private static readonly Regex TagRegex = new("<[^>]+>", RegexOptions.Compiled);
    private static readonly Regex MultiWhitespaceRegex = new("\\s+", RegexOptions.Compiled);

    private readonly HtmlSanitizer _sanitizer = new();

    public string Sanitize(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return string.Empty;
        }

        return _sanitizer.Sanitize(html.Trim());
    }

    public string ToPlainText(string? html)
    {
        var sanitized = Sanitize(html);
        if (string.IsNullOrWhiteSpace(sanitized))
        {
            return string.Empty;
        }

        var withoutTags = TagRegex.Replace(sanitized, " ");
        var decoded = WebUtility.HtmlDecode(withoutTags);
        return MultiWhitespaceRegex.Replace(decoded, " ").Trim();
    }
}

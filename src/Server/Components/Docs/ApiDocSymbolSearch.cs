using Server.Contracts.ApiDocumentation;

namespace Server.Components.Docs;

/// <summary>Client-side symbol filtering and ranking for package API documentation search.</summary>
public static class ApiDocSymbolSearch
{
    public const int NoMatchScore = int.MinValue;

    public static bool Matches(StructuredApiItemDto item, string query)
        => Score(item, query) > NoMatchScore;

    /// <summary>Higher scores sort first. Prefix qualified-name matches rank above substring; prose fields rank below identifiers.</summary>
    public static int Score(StructuredApiItemDto item, string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return 0;
        }

        var q = query.Trim();
        var qn = item.QualifiedName ?? string.Empty;
        var name = item.Name ?? string.Empty;
        var kind = item.Kind ?? string.Empty;
        var signature = item.Signature ?? string.Empty;
        var summary = item.Doc?.SummaryMarkdown ?? string.Empty;
        var markdown = item.DocMarkdown ?? string.Empty;

        if (qn.StartsWith(q, StringComparison.OrdinalIgnoreCase))
        {
            return 1000 - Math.Min(qn.Length, 200);
        }

        if (name.StartsWith(q, StringComparison.OrdinalIgnoreCase))
        {
            return 900 - Math.Min(name.Length, 200);
        }

        if (qn.Contains(q, StringComparison.OrdinalIgnoreCase))
        {
            return 700;
        }

        if (name.Contains(q, StringComparison.OrdinalIgnoreCase))
        {
            return 650;
        }

        if (kind.Contains(q, StringComparison.OrdinalIgnoreCase))
        {
            return 500;
        }

        if (signature.Contains(q, StringComparison.OrdinalIgnoreCase))
        {
            return 400;
        }

        if (summary.Contains(q, StringComparison.OrdinalIgnoreCase))
        {
            return 300;
        }

        if (markdown.Contains(q, StringComparison.OrdinalIgnoreCase))
        {
            return 250;
        }

        return NoMatchScore;
    }
}

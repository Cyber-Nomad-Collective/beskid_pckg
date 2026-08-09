using System.Text.RegularExpressions;
using Markdig.Helpers;
using Server.Contracts.ApiDocumentation;
using Server.Services;

namespace Server.Components.Docs;

public partial class PackageDocs
{
    private IReadOnlyList<PackageDocsBreadcrumb> MemberBreadcrumbs =>
        BuildMemberBreadcrumbs(_selected);

    private string? SelectedMemberPageUrl
    {
        get
        {
            if (_selected is null)
            {
                return null;
            }

            var qn = _selected.QualifiedName ?? _selected.Name;
            if (string.IsNullOrWhiteSpace(qn))
            {
                return null;
            }

            var relative = AppDocumentationRoutes.AppDocsApiMember(
                PackageIdentifier.Trim(),
                Version.Trim(),
                qn
            );
            return Navigation.ToAbsoluteUri(relative).AbsoluteUri;
        }
    }

    private IReadOnlyList<PackageDocsTocRow> TocRows => BuildTocRows(NarrativeDocMarkdown);

    private IReadOnlyList<StructuredApiItemDto> SelectedMemberChildren
    {
        get
        {
            if (_selected?.MemberIds is not { Count: > 0 } ids)
            {
                return [];
            }

            var list = new List<StructuredApiItemDto>();
            foreach (var id in ids)
            {
                if (_itemsById.TryGetValue(id, out var child))
                {
                    list.Add(child);
                }
            }

            return list;
        }
    }

    /// <summary>Markdown body when structured summary duplicates the full doc block.</summary>
    private string? NarrativeDocMarkdown
    {
        get
        {
            if (_selected is null)
            {
                return null;
            }

            var full = _selected.DocMarkdown?.Trim();
            var summary = _selected.Doc?.SummaryMarkdown?.Trim();
            if (
                full is null
                || summary is null
                || !string.Equals(full, summary, StringComparison.Ordinal)
            )
            {
                return _selected.DocMarkdown;
            }

            return null;
        }
    }

    private IReadOnlyList<PackageDocsBreadcrumb> BuildMemberBreadcrumbs(StructuredApiItemDto? item)
    {
        if (item is null)
        {
            return [];
        }

        var chain = new List<StructuredApiItemDto>();
        var guard = 0;
        var cur = item;
        chain.Add(cur);
        while (
            cur.ParentId is int pid && guard++ < 4096 && _itemsById.TryGetValue(pid, out var parent)
        )
        {
            chain.Add(parent);
            cur = parent;
        }

        chain.Reverse();
        return chain
            .Select(
                (node, index) =>
                {
                    var label = node.Name ?? node.QualifiedName ?? "?";
                    var isLast = index == chain.Count - 1;
                    return new PackageDocsBreadcrumb(label, node, Selectable: !isLast);
                }
            )
            .ToList();
    }

    private string ParentDisplayLabel(StructuredApiItemDto x)
    {
        if (x.ParentId is not int p || !_itemsById.TryGetValue(p, out var par))
        {
            return "-";
        }

        return par.Name ?? par.QualifiedName ?? p.ToString();
    }

    private string? FormatSymbolLocation(StructuredApiItemDto item)
    {
        if (item.Location is not { } loc)
        {
            return null;
        }

        if (
            !string.IsNullOrWhiteSpace(item.DeclaringPackage)
            && !string.Equals(
                item.DeclaringPackage.Trim(),
                PackageIdentifier.Trim(),
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            return item.DeclaringPackage.Trim();
        }

        return $"{loc.File}:{loc.StartLine}";
    }

    private static IReadOnlyList<PackageDocsTocRow> BuildTocRows(string? markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return [];
        }

        var rows = new List<PackageDocsTocRow>();
        var slugCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        var matches = Regex.Matches(markdown, @"^(#{2,4})\s+(.+)$", RegexOptions.Multiline);
        foreach (Match match in matches)
        {
            if (!match.Success)
            {
                continue;
            }

            var level = match.Groups[1].Value.Length;
            var title = match.Groups[2].Value.Trim();
            var slug = Slugify(title);
            if (string.IsNullOrWhiteSpace(slug))
            {
                continue;
            }

            if (!slugCounts.TryAdd(slug, 0))
            {
                slugCounts[slug]++;
                slug = $"{slug}-{slugCounts[slug]}";
            }

            rows.Add(new PackageDocsTocRow(level, title, slug));
        }

        return rows;
    }

    private static string Slugify(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        // Match Markdig `UseAutoIdentifiers(AutoIdentifierOptions.GitHub)` heading ids.
        return LinkHelper.UrilizeAsGfm(value.Trim());
    }
}

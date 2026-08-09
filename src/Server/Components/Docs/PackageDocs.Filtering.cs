using Server.Contracts.ApiDocumentation;

namespace Server.Components.Docs;

public partial class PackageDocs
{
    private IEnumerable<string> AvailableKinds =>
        _doc is null
            ? []
            : _doc
                .Items.Select(x => (x.Kind ?? string.Empty).Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase);

    private IEnumerable<StructuredApiItemDto> ScopeRootOptions =>
        _doc is null ? [] : ApiDocNavigationBuilder.ModuleScopeRootCandidates(_doc);

    private IEnumerable<StructuredApiItemDto> FilteredItems
    {
        get
        {
            if (_doc is null)
            {
                return [];
            }

            var query = _symbolQuery.Trim();
            var selectedKinds = _kindFilters.Count == 0 ? null : _kindFilters;
            var scopeId = ParseScopeRootId(_scopeRootId);
            var ranked = _doc
                .Items.Where(x =>
                {
                    if (!string.IsNullOrWhiteSpace(query) && !ApiDocSymbolSearch.Matches(x, query))
                    {
                        return false;
                    }

                    if (scopeId is int rid && !ItemIsUnderScopeRoot(x, rid))
                    {
                        return false;
                    }

                    if (selectedKinds is not null)
                    {
                        var kind = (x.Kind ?? string.Empty).Trim();
                        if (!selectedKinds.Contains(kind))
                        {
                            return false;
                        }
                    }

                    return true;
                })
                .Select(x =>
                    (
                        Item: x,
                        Score: string.IsNullOrWhiteSpace(query)
                            ? 0
                            : ApiDocSymbolSearch.Score(x, query)
                    )
                )
                .OrderByDescending(x => x.Score)
                .ThenBy(x => x.Item.QualifiedName ?? x.Item.Name, StringComparer.Ordinal)
                .Select(x => x.Item);
            return ranked;
        }
    }

    private IReadOnlyList<PackageDocsSymbolRow> SymbolRows =>
        FilteredItems
            .Select(x => new PackageDocsSymbolRow(
                x.Id,
                x.QualifiedName ?? x.Name ?? string.Empty,
                string.IsNullOrWhiteSpace(x.Kind) ? "unknown" : x.Kind!,
                ParentDisplayLabel(x),
                string.IsNullOrWhiteSpace(x.Visibility) ? "-" : x.Visibility!,
                FormatSymbolLocation(x),
                x
            ))
            .ToList();

    private bool HasActiveNavFilters =>
        !string.IsNullOrWhiteSpace(_symbolQuery)
        || _kindFilters.Count > 0
        || !string.IsNullOrWhiteSpace(_scopeRootId);

    private IReadOnlyList<GraphNavNode> DisplayNavRoots
    {
        get
        {
            if (!HasActiveNavFilters)
            {
                return _navRoots;
            }

            var visible = new HashSet<int>();
            foreach (var item in FilteredItems)
            {
                if (item.Id is not int id)
                {
                    continue;
                }

                visible.Add(id);
                var guard = 0;
                var cur = item;
                while (cur.ParentId is int pid && guard++ < 4096)
                {
                    if (!visible.Add(pid))
                    {
                        break;
                    }

                    if (!_itemsById.TryGetValue(pid, out cur))
                    {
                        break;
                    }
                }
            }

            return ApiDocNavigationBuilder.FilterGraphRoots(_navRoots, visible);
        }
    }

    private bool ItemIsUnderScopeRoot(StructuredApiItemDto item, int rootId)
    {
        if (item.Id == rootId)
        {
            return true;
        }

        var guard = 0;
        var cur = item;
        while (cur.ParentId is int p && guard++ < 4096)
        {
            if (p == rootId)
            {
                return true;
            }

            if (!_itemsById.TryGetValue(p, out cur))
            {
                return false;
            }
        }

        return false;
    }

    private static int? ParseScopeRootId(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        return int.TryParse(raw, out var id) ? id : null;
    }

    private void SetKindSelected(string kind, bool selected)
    {
        if (string.IsNullOrWhiteSpace(kind))
        {
            return;
        }

        if (selected)
        {
            _kindFilters.Add(kind);
        }
        else
        {
            _kindFilters.Remove(kind);
        }
    }

    private bool IsKindSelected(string kind)
    {
        if (string.IsNullOrWhiteSpace(kind))
        {
            return false;
        }

        return _kindFilters.Contains(kind);
    }

    private void SetScopeRootFilter(string? raw)
    {
        _scopeRootId = raw ?? string.Empty;
    }

    private void ClearFilters()
    {
        _scopeRootId = string.Empty;
        _kindFilters.Clear();
    }
}

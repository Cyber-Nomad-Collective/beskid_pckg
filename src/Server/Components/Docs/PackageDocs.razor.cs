using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Markdig.Helpers;
using Microsoft.AspNetCore.Components;
using Server.Contracts.ApiDocumentation;
using Server.Features.Packages;
using Server.Services;

namespace Server.Components.Docs;

public partial class PackageDocs
{
    public enum PackageDocsVariant
    {
        Embedded,
        FullPage
    }

    [Parameter] public string PackageIdentifier { get; set; } = string.Empty;
    [Parameter] public string Version { get; set; } = "latest";
    [Parameter] public string? DeepLinkQualifiedName { get; set; }
    [Parameter] public string? InitialSymbolSearch { get; set; }
    [Parameter] public PackageDocsVariant Variant { get; set; } = PackageDocsVariant.Embedded;

    [Inject] private HttpClient Http { get; set; } = default!;

    private bool _loading = true;
    private string? _loadError;
    private StructuredApiDocDto? _doc;
    private Dictionary<int, StructuredApiItemDto> _itemsById = [];
    private IReadOnlyList<GraphNavNode> _navRoots = [];
    private StructuredApiItemDto? _selected;
    private string? _deepLinkMissMessage;
    private string _symbolQuery = string.Empty;
    /// <summary>Root item id for graph scope filter, or empty for all.</summary>
    private string _scopeRootId = string.Empty;
    private readonly HashSet<string> _kindFilters = new(StringComparer.OrdinalIgnoreCase);
    private bool _showMorePanel;
    private string? _fetchKey;
    private string? _appliedDeepLink;
    private string? _appliedInitialSymbol;

    private IEnumerable<string> AvailableKinds =>
        _doc is null
            ? []
            : _doc.Items
                .Select(x => (x.Kind ?? string.Empty).Trim())
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
            return _doc.Items.Where(x =>
                {
                    if (!string.IsNullOrWhiteSpace(query))
                    {
                        var qn = x.QualifiedName ?? string.Empty;
                        var name = x.Name ?? string.Empty;
                        var kind = x.Kind ?? string.Empty;
                        if (!qn.Contains(query, StringComparison.OrdinalIgnoreCase)
                            && !name.Contains(query, StringComparison.OrdinalIgnoreCase)
                            && !kind.Contains(query, StringComparison.OrdinalIgnoreCase))
                        {
                            return false;
                        }
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
                .OrderBy(x => x.QualifiedName ?? x.Name, StringComparer.Ordinal);
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
                x.Location is null ? null : $"{x.Location.File}:{x.Location.StartLine}",
                x))
            .ToList();

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
            if (full is null || summary is null || !string.Equals(full, summary, StringComparison.Ordinal))
            {
                return _selected.DocMarkdown;
            }

            return null;
        }
    }

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

    protected override async Task OnParametersSetAsync()
    {
        if (string.IsNullOrWhiteSpace(PackageIdentifier) || string.IsNullOrWhiteSpace(Version))
        {
            _loading = false;
            _loadError = "Package or version is missing.";
            return;
        }

        var key = $"{PackageIdentifier.Trim()}|{Version.Trim()}";
        var deep = string.IsNullOrWhiteSpace(DeepLinkQualifiedName) ? null : DeepLinkQualifiedName.Trim();
        var symInit = string.IsNullOrWhiteSpace(InitialSymbolSearch) ? null : InitialSymbolSearch.Trim();

        if (_fetchKey == key && _doc is not null)
        {
            if (!string.Equals(_appliedDeepLink, deep, StringComparison.Ordinal)
                || !string.Equals(_appliedInitialSymbol, symInit, StringComparison.Ordinal))
            {
                _appliedDeepLink = deep;
                _appliedInitialSymbol = symInit;
                ApplyDeepLinkAndSearchFromParams();
            }

            return;
        }

        _fetchKey = key;
        _appliedDeepLink = deep;
        _appliedInitialSymbol = symInit;
        await LoadStructuredAsync();
    }

    private async Task LoadStructuredAsync()
    {
        _loading = true;
        _loadError = null;
        _doc = null;
        _itemsById = [];
        _navRoots = [];
        _selected = null;
        _deepLinkMissMessage = null;
        _showMorePanel = false;
        _kindFilters.Clear();
        _scopeRootId = string.Empty;
        _symbolQuery = string.IsNullOrWhiteSpace(InitialSymbolSearch) ? string.Empty : InitialSymbolSearch.Trim();
        try
        {
            var url = PackageDocumentationUrls.DocsStructured(PackageIdentifier.Trim(), Version.Trim());
            var response = await Http.GetAsync(url);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                _loadError = "Structured API documentation was not found for this version.";
                return;
            }

            if (!response.IsSuccessStatusCode)
            {
                _loadError = "Could not load structured documentation.";
                return;
            }

            var json = await response.Content.ReadAsStringAsync();
            var doc = JsonSerializer.Deserialize<StructuredApiDocDto>(json, StructuredApiDocJson.Options);
            if (doc is null || doc.Items.Count == 0)
            {
                _loadError = "Structured documentation is empty.";
                return;
            }

            if (!ApiDocNavigationBuilder.SupportsStructuredGraph(doc))
            {
                _loadError =
                    "This package ships legacy api.json without graph navigation. Re-publish with a Beskid CLI that emits schemaVersion 4 (or 3) and navigationModel graph-v1.";
                return;
            }

            _doc = doc;
            _itemsById = doc.Items.Where(i => i.Id is not null).ToDictionary(i => i.Id!.Value);
            _navRoots = ApiDocNavigationBuilder.BuildGraphRoots(doc);
            _selected = doc.Items.Where(i => i.Id is not null).OrderBy(i => i.Id).FirstOrDefault();
            ApplyDeepLinkAndSearchFromParams();
        }
        catch
        {
            _loadError = "Could not load structured documentation (network or parse error).";
        }
        finally
        {
            _loading = false;
        }
    }

    private void ApplyDeepLinkAndSearchFromParams()
    {
        _deepLinkMissMessage = null;
        if (_doc is null)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(DeepLinkQualifiedName))
        {
            var target = DeepLinkQualifiedName.Trim();
            var match = _doc.Items.FirstOrDefault(x =>
                string.Equals(x.QualifiedName ?? x.Name, target, StringComparison.Ordinal)
                || string.Equals(x.Name, target, StringComparison.Ordinal)
                || string.Equals(x.QualifiedName, target, StringComparison.Ordinal));
            if (match is not null)
            {
                _selected = match;
            }
            else
            {
                _deepLinkMissMessage = $"No API member matched the qualified name \"{target}\".";
                _selected ??= _doc.Items.Where(i => i.Id is not null).OrderBy(i => i.Id).FirstOrDefault();
            }
        }
        else if (_selected is null)
        {
            _selected = _doc.Items.Where(i => i.Id is not null).OrderBy(i => i.Id).FirstOrDefault();
        }

        if (!string.IsNullOrWhiteSpace(InitialSymbolSearch))
        {
            _symbolQuery = InitialSymbolSearch.Trim();
        }
    }

    private Task SelectItemByIdAsync(int itemId)
    {
        if (_itemsById.TryGetValue(itemId, out var item))
        {
            return SelectItemAsync(item);
        }

        return Task.CompletedTask;
    }

    private Task SelectItemAsync(StructuredApiItemDto item)
    {
        _selected = item;
        return Task.CompletedTask;
    }

    private string ParentDisplayLabel(StructuredApiItemDto x)
    {
        if (x.ParentId is not int p || !_itemsById.TryGetValue(p, out var par))
        {
            return "-";
        }

        return par.Name ?? par.QualifiedName ?? p.ToString();
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

    private void ToggleMorePanel() => _showMorePanel = !_showMorePanel;

    private void CloseMorePanel() => _showMorePanel = false;

    private void ToggleKindFilter(string kind)
    {
        if (string.IsNullOrWhiteSpace(kind))
        {
            return;
        }

        if (!_kindFilters.Add(kind))
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

using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Markdig.Helpers;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using Server.Contracts.ApiDocumentation;
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
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private IJSRuntime Js { get; set; } = default!;

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
    private readonly HashSet<int> _expandedNavIds = [];

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
            var ranked = _doc.Items.Where(x =>
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
                .Select(x => (Item: x, Score: string.IsNullOrWhiteSpace(query) ? 0 : ApiDocSymbolSearch.Score(x, query)))
                .OrderByDescending(x => x.Score)
                .ThenBy(x => x.Item.QualifiedName ?? x.Item.Name, StringComparer.Ordinal)
                .Select(x => x.Item);
            return ranked;
        }
    }

    private IReadOnlyList<PackageDocsBreadcrumb> MemberBreadcrumbs => BuildMemberBreadcrumbs(_selected);

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
                qn);
            return Navigation.ToAbsoluteUri(relative).AbsoluteUri;
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

            if (response.StatusCode == HttpStatusCode.RequestEntityTooLarge)
            {
                _loadError =
                    "Structured API documentation exceeds the registry size limit. Re-publish with a smaller api.json or contact the registry operator.";
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
            _navRoots = ApiDocNavigationBuilder.BuildLibraryTreeRoots(doc, PackageIdentifier.Trim());
            _expandedNavIds.Clear();
            foreach (var root in _navRoots)
            {
                _expandedNavIds.Add(root.ExpansionKey);
            }

            _selected = FindDefaultSelection(doc) ?? doc.Items.Where(i => i.Id is not null).OrderBy(i => i.Id).FirstOrDefault();
            ApplyDeepLinkAndSearchFromParams();
            EnsureExpandedForItem(_selected);
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
                EnsureExpandedForItem(match);
            }
            else
            {
                _deepLinkMissMessage = $"No API member matched the qualified name \"{target}\".";
                _selected ??= _doc.Items.Where(i => i.Id is not null).OrderBy(i => i.Id).FirstOrDefault();
                EnsureExpandedForItem(_selected);
            }
        }
        else if (_selected is null)
        {
            _selected = _doc.Items.Where(i => i.Id is not null).OrderBy(i => i.Id).FirstOrDefault();
            EnsureExpandedForItem(_selected);
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
        EnsureExpandedForItem(item);
        SyncBrowserLocationForSelection(item);
        return Task.CompletedTask;
    }

    private Task ToggleNavExpandAsync(int itemId)
    {
        if (_expandedNavIds.Contains(itemId))
        {
            _expandedNavIds.Remove(itemId);
        }
        else
        {
            _expandedNavIds.Add(itemId);
        }

        return Task.CompletedTask;
    }

    private void EnsureExpandedForItem(StructuredApiItemDto? item)
    {
        if (item is null)
        {
            return;
        }

        ExpandNavPathToItem(item.Id);
    }

    private void ExpandNavPathToItem(int? itemId)
    {
        if (itemId is null || !TryFindNavPath(_navRoots, itemId.Value, out var path))
        {
            if (itemId is int id)
            {
                var guard = 0;
                var curId = (int?)id;
                while (curId is int pid && guard++ < 4096)
                {
                    _expandedNavIds.Add(pid);
                    if (!_itemsById.TryGetValue(pid, out var parent))
                    {
                        break;
                    }

                    curId = parent.ParentId;
                }

                _expandedNavIds.Add(id);
            }

            return;
        }

        foreach (var key in path)
        {
            _expandedNavIds.Add(key);
        }
    }

    private static bool TryFindNavPath(
        IReadOnlyList<GraphNavNode> nodes,
        int targetItemId,
        out List<int> expansionKeys)
    {
        foreach (var node in nodes)
        {
            if (TryFindNavPathCore(node, targetItemId, [], out expansionKeys))
            {
                return true;
            }
        }

        expansionKeys = [];
        return false;
    }

    private static bool TryFindNavPathCore(
        GraphNavNode node,
        int targetItemId,
        List<int> prefix,
        out List<int> expansionKeys)
    {
        prefix.Add(node.ExpansionKey);
        if (node.Item?.Id == targetItemId)
        {
            expansionKeys = prefix;
            return true;
        }

        foreach (var child in node.Children)
        {
            if (TryFindNavPathCore(child, targetItemId, [..prefix], out expansionKeys))
            {
                return true;
            }
        }

        expansionKeys = [];
        return false;
    }

    private static StructuredApiItemDto? FindDefaultSelection(StructuredApiDocDto doc)
    {
        var module = doc.Items.FirstOrDefault(i =>
            string.Equals(i.Kind, "module", StringComparison.OrdinalIgnoreCase)
            && i.ParentId is null);
        if (module is not null)
        {
            return module;
        }

        return doc.Items.FirstOrDefault(i =>
            i.ParentId is null
            && (string.Equals(i.Kind, "type", StringComparison.OrdinalIgnoreCase)
                || string.Equals(i.Kind, "enum", StringComparison.OrdinalIgnoreCase)
                || string.Equals(i.Kind, "contract", StringComparison.OrdinalIgnoreCase)));
    }

    private void SyncBrowserLocationForSelection(StructuredApiItemDto item)
    {
        if (Variant != PackageDocsVariant.FullPage)
        {
            return;
        }

        var qn = item.QualifiedName ?? item.Name;
        if (string.IsNullOrWhiteSpace(qn))
        {
            return;
        }

        var target = AppDocumentationRoutes.AppDocsApiMember(
            PackageIdentifier.Trim(),
            Version.Trim(),
            qn);
        var current = Navigation.ToBaseRelativePath(Navigation.Uri);
        if (string.Equals(current, target.TrimStart('/'), StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        Navigation.NavigateTo(target, replace: true);
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
        while (cur.ParentId is int pid && guard++ < 4096 && _itemsById.TryGetValue(pid, out var parent))
        {
            chain.Add(parent);
            cur = parent;
        }

        chain.Reverse();
        return chain
            .Select((node, index) =>
            {
                var label = node.Name ?? node.QualifiedName ?? "?";
                var isLast = index == chain.Count - 1;
                return new PackageDocsBreadcrumb(label, node, Selectable: !isLast);
            })
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

        if (!string.IsNullOrWhiteSpace(item.DeclaringPackage)
            && !string.Equals(
                item.DeclaringPackage.Trim(),
                PackageIdentifier.Trim(),
                StringComparison.OrdinalIgnoreCase))
        {
            return item.DeclaringPackage.Trim();
        }

        return $"{loc.File}:{loc.StartLine}";
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

    private void ToggleMorePanel()
    {
        _showMorePanel = !_showMorePanel;
        if (_showMorePanel)
        {
            _ = PositionFilterPopoverAsync();
        }
    }

    private void CloseMorePanel() => _showMorePanel = false;

    private async Task SelectSymbolFromPanelAsync(StructuredApiItemDto item)
    {
        await SelectItemAsync(item);
        CloseMorePanel();
    }

    private async Task PositionFilterPopoverAsync()
    {
        try
        {
            await Js.InvokeVoidAsync("pckgDocs.positionFilterPopover");
        }
        catch
        {
            // Ignore when JS interop is unavailable during prerender.
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_showMorePanel)
        {
            await PositionFilterPopoverAsync();
        }
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

    private async Task HandleDocsKeyDown(KeyboardEventArgs e)
    {
        if (!string.Equals(e.Key, "/", StringComparison.Ordinal)
            || e.CtrlKey
            || e.MetaKey
            || e.AltKey)
        {
            return;
        }

        try
        {
            await Js.InvokeVoidAsync("pckgDocs.focusSymbolSearch");
        }
        catch
        {
            // Ignore when JS interop is unavailable during prerender.
        }
    }
}

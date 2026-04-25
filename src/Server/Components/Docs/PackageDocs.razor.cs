using System.Net;
using System.Text.RegularExpressions;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
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

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private bool _loading = true;
    private string? _loadError;
    private StructuredApiDocDto? _doc;
    private StructuredApiItemDto? _selected;
    private string? _deepLinkMissMessage;
    private string _symbolQuery = string.Empty;
    private string _moduleFilter = string.Empty;
    private readonly HashSet<string> _kindFilters = new(StringComparer.OrdinalIgnoreCase);
    private string _groupBy = "module";
    private bool _showFilterPopover;
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

    private IEnumerable<string> AvailableModules =>
        _doc is null
            ? []
            : _doc.Items
                .Select(ModuleName)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(x => x, StringComparer.Ordinal);

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
            var selectedModule = _moduleFilter.Trim();
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

                    if (!string.IsNullOrWhiteSpace(selectedModule)
                        && !string.Equals(ModuleName(x), selectedModule, StringComparison.Ordinal))
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
                ModuleName(x),
                string.IsNullOrWhiteSpace(x.Visibility) ? "-" : x.Visibility!,
                x.Location is null ? null : $"{x.Location.File}:{x.Location.StartLine}",
                x))
            .ToList();

    private IReadOnlyList<PackageDocsTocRow> TocRows => BuildTocRows(_selected?.DocMarkdown);

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
        _selected = null;
        _deepLinkMissMessage = null;
        _showFilterPopover = false;
        _kindFilters.Clear();
        _moduleFilter = string.Empty;
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
            var doc = JsonSerializer.Deserialize<StructuredApiDocDto>(json, JsonOptions);
            if (doc is null || doc.Items.Count == 0)
            {
                _loadError = "Structured documentation is empty.";
                return;
            }

            _doc = doc;
            _selected = doc.Items
                .OrderBy(x => x.QualifiedName ?? x.Name, StringComparer.Ordinal)
                .FirstOrDefault();
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
                _selected ??= _doc.Items
                    .OrderBy(x => x.QualifiedName ?? x.Name, StringComparer.Ordinal)
                    .FirstOrDefault();
            }
        }
        else if (_selected is null)
        {
            _selected = _doc.Items
                .OrderBy(x => x.QualifiedName ?? x.Name, StringComparer.Ordinal)
                .FirstOrDefault();
        }

        if (!string.IsNullOrWhiteSpace(InitialSymbolSearch))
        {
            _symbolQuery = InitialSymbolSearch.Trim();
        }
    }

    private Task SelectItemAsync(StructuredApiItemDto item)
    {
        _selected = item;
        return Task.CompletedTask;
    }

    private Task NavigateToQualifiedNameAsync(string name)
    {
        if (_doc is null || string.IsNullOrWhiteSpace(name))
        {
            return Task.CompletedTask;
        }

        var match = _doc.Items.FirstOrDefault(x =>
            string.Equals(x.QualifiedName ?? x.Name, name, StringComparison.Ordinal));
        if (match is not null)
        {
            _selected = match;
        }

        return Task.CompletedTask;
    }

    private static string ModuleName(StructuredApiItemDto item)
    {
        var q = item.QualifiedName ?? item.Name ?? string.Empty;
        var segs = q.Split("::", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return segs.Length > 0 ? segs[0] : "global";
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

        var slug = value.Trim().ToLowerInvariant();
        slug = Regex.Replace(slug, @"[`*_~\[\]\(\)\.,:;!?\\""']", string.Empty);
        slug = Regex.Replace(slug, @"\s+", "-");
        slug = Regex.Replace(slug, @"-+", "-");
        return slug.Trim('-');
    }

    private void ToggleFilterPopover()
    {
        _showFilterPopover = !_showFilterPopover;
    }

    private void CloseFilterPopover()
    {
        _showFilterPopover = false;
    }

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

    private void SetModuleFilter(string? module)
    {
        _moduleFilter = module ?? string.Empty;
    }

    private void ClearFilters()
    {
        _moduleFilter = string.Empty;
        _kindFilters.Clear();
        _groupBy = "module";
    }

    private void SetGroupBy(string? value)
    {
        _groupBy = string.Equals(value, "kind", StringComparison.OrdinalIgnoreCase) ? "kind" : "module";
    }
}

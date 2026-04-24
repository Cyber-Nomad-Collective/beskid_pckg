using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Server.Features.Packages;
using Server.Services;

namespace Server.Components.Docs;

public partial class StructuredPackageDocShell
{
    [Parameter]
    public string PackageIdentifier { get; set; } = string.Empty;

    [Parameter]
    public string Version { get; set; } = "latest";

    [Parameter]
    public PackageDocsIndexResponse? MarkdownIndex { get; set; }

    /// <summary>Select this member after load (ordinal match on <see cref="StructuredApiItemDto.QualifiedName"/> or <see cref="StructuredApiItemDto.Name"/>).</summary>
    [Parameter]
    public string? DeepLinkQualifiedName { get; set; }

    /// <summary>Pre-fill symbol search (filter on qualified name, name, kind).</summary>
    [Parameter]
    public string? InitialSymbolSearch { get; set; }

    [Inject]
    private HttpClient Http { get; set; } = default!;

    [Inject]
    private IMarkdownService MarkdownService { get; set; } = default!;

    [Inject]
    private IHtmlSanitizationService HtmlSanitization { get; set; } = default!;

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
    private bool _showMarkdownPages;
    private string? _mdPath;
    private bool _loadingMd;
    private MarkupString? _mdRendered;
    private string? _deepLinkMissMessage;
    private string _symbolQuery = string.Empty;
    private string? _fetchKey;
    private string? _appliedDeepLink;
    private string? _appliedInitialSymbol;

    private IEnumerable<StructuredApiItemDto> SymbolMatches
    {
        get
        {
            if (_doc is null || string.IsNullOrWhiteSpace(_symbolQuery))
            {
                return [];
            }

            var q = _symbolQuery.Trim();
            return _doc.Items
                .Where(x =>
                {
                    var qn = x.QualifiedName ?? string.Empty;
                    var name = x.Name ?? string.Empty;
                    var kind = x.Kind ?? string.Empty;
                    return qn.Contains(q, StringComparison.OrdinalIgnoreCase)
                           || name.Contains(q, StringComparison.OrdinalIgnoreCase)
                           || kind.Contains(q, StringComparison.OrdinalIgnoreCase);
                })
                .OrderBy(x => x.QualifiedName ?? x.Name, StringComparer.Ordinal);
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
        _selected = null;
        _deepLinkMissMessage = null;
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
                _deepLinkMissMessage = $"No API member matched the qualified name “{target}”.";
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

    private async Task SelectMarkdownAsync(string path)
    {
        _mdPath = path;
        _loadingMd = true;
        _mdRendered = null;
        try
        {
            var url = PackageDocumentationUrls.DocsFile(PackageIdentifier.Trim(), Version.Trim(), path);
            var response = await Http.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                _mdRendered = new MarkupString("<p class=\"muted\">Could not load this page.</p>");
                return;
            }

            var text = await response.Content.ReadAsStringAsync();
            var html = MarkdownService.ToSafeHtml(text);
            _mdRendered = new MarkupString(HtmlSanitization.Sanitize(html));
        }
        catch
        {
            _mdRendered = new MarkupString("<p class=\"muted\">Could not load this page.</p>");
        }
        finally
        {
            _loadingMd = false;
        }
    }
}

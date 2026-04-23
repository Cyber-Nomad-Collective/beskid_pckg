using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.FluentUI.AspNetCore.Components;
using Server.Features.Packages;
using Server.Services;

namespace Server.Components.Shared;

public enum PackageDocsBrowserVariant
{
    Embedded,
    FullPage
}

public partial class PackageDocsBrowser
{
    [Parameter] public string PackageIdentifier { get; set; } = string.Empty;
    [Parameter] public string Version { get; set; } = "latest";
    [Parameter] public PackageDocsBrowserVariant Variant { get; set; } = PackageDocsBrowserVariant.Embedded;

    [Inject] private HttpClient Http { get; set; } = default!;
    [Inject] private IMarkdownService MarkdownService { get; set; } = default!;
    [Inject] private IHtmlSanitizationService HtmlSanitization { get; set; } = default!;

    private readonly List<PackageDocFileEntry> _allFiles = [];
    private string? _loadError;
    private bool _loadingIndex = true;
    private bool _loadingDoc;
    private string _search = string.Empty;
    private string? _cachedPackage;
    private string? _cachedVersion;
    private string? _selectedPath;
    private string? _selectedTitle;
    private string? _rawMarkdown;
    private MarkupString _renderedDoc;

    private IEnumerable<PackageDocFileEntry> FilteredFiles =>
        string.IsNullOrWhiteSpace(_search)
            ? _allFiles
            : _allFiles.Where(f =>
                f.Path.Contains(_search, StringComparison.OrdinalIgnoreCase)
                || f.Title.Contains(_search, StringComparison.OrdinalIgnoreCase));

    private static string ApiIndexUrl(string packageId, string version) =>
        $"/api/packages/{Uri.EscapeDataString(packageId)}/versions/{Uri.EscapeDataString(version)}/docs";

    private static string ApiFileUrl(string packageId, string version, string path) =>
        $"/api/packages/{Uri.EscapeDataString(packageId)}/versions/{Uri.EscapeDataString(version)}/docs/file?path={Uri.EscapeDataString(path)}";

    protected override async Task OnParametersSetAsync()
    {
        if (string.IsNullOrWhiteSpace(PackageIdentifier) || string.IsNullOrWhiteSpace(Version))
        {
            _loadError = "Package or version is missing.";
            _loadingIndex = false;
            return;
        }

        var pkg = PackageIdentifier.Trim();
        var ver = Version.Trim();
        if (string.Equals(_cachedPackage, pkg, StringComparison.Ordinal)
            && string.Equals(_cachedVersion, ver, StringComparison.Ordinal))
        {
            return;
        }

        _cachedPackage = pkg;
        _cachedVersion = ver;
        await LoadIndexAsync();
    }

    private async Task LoadIndexAsync()
    {
        _loadingIndex = true;
        _loadError = null;
        _allFiles.Clear();
        _selectedPath = null;
        _selectedTitle = null;
        _rawMarkdown = null;
        _renderedDoc = default;

        try
        {
            var response = await Http.GetAsync(ApiIndexUrl(PackageIdentifier.Trim(), Version.Trim()));
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                _loadError = "Documentation was not found for this package version.";
                return;
            }

            if (!response.IsSuccessStatusCode)
            {
                _loadError = "Could not load documentation index.";
                return;
            }

            var payload = await response.Content.ReadFromJsonAsync<PackageDocsIndexResponse>();
            if (payload?.Files is { Count: > 0 } files)
            {
                _allFiles.AddRange(files);
                var first = _allFiles[0];
                _selectedPath = first.Path;
                _selectedTitle = first.Title;
                await LoadSelectedDocAsync();
            }
            else
            {
                _loadError = "This package version does not include any docs/ markdown files or README.md.";
            }
        }
        catch
        {
            _loadError = "Could not load documentation.";
        }
        finally
        {
            _loadingIndex = false;
        }
    }

    private async Task SelectPathAsync(string path)
    {
        if (string.Equals(_selectedPath, path, StringComparison.Ordinal))
        {
            return;
        }

        _selectedPath = path;
        _selectedTitle = _allFiles.FirstOrDefault(f => string.Equals(f.Path, path, StringComparison.Ordinal))?.Title
                          ?? path.Split('/').LastOrDefault()
                          ?? path;
        await LoadSelectedDocAsync();
    }

    private async Task LoadSelectedDocAsync()
    {
        if (string.IsNullOrWhiteSpace(_selectedPath))
        {
            return;
        }

        _loadingDoc = true;
        _rawMarkdown = null;
        _renderedDoc = default;
        StateHasChanged();

        try
        {
            var response = await Http.GetAsync(ApiFileUrl(PackageIdentifier.Trim(), Version.Trim(), _selectedPath));
            if (!response.IsSuccessStatusCode)
            {
                _renderedDoc = new MarkupString("<p class=\"muted\">Could not load this page.</p>");
                return;
            }

            var text = await response.Content.ReadAsStringAsync();
            _rawMarkdown = text;
            var html = MarkdownService.ToSafeHtml(text);
            _renderedDoc = new MarkupString(HtmlSanitization.Sanitize(html));
        }
        catch
        {
            _renderedDoc = new MarkupString("<p class=\"muted\">Could not load this page.</p>");
        }
        finally
        {
            _loadingDoc = false;
        }
    }

    private static string RootClass(PackageDocsBrowserVariant v) =>
        v == PackageDocsBrowserVariant.FullPage ? "docs-browser-root docs-browser-root--full" : "docs-browser-root docs-browser-root--embedded";

    private static string ShortPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        return path.Length <= 42 ? path : path[..19] + "…" + path[^18..];
    }
}

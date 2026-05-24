using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using Server.Data;
using Server.Features.Packages;
using Server.Services;

namespace Server.Components.Pages;

public partial class DocsView
{
    [Parameter] public string PackageWithVersion { get; set; } = string.Empty;

    /// <summary>Route segment from <c>/docs/{{pkg@ver}}/api/{{QualifiedName}}</c>; use <see cref="Uri.EscapeDataString"/> when building links.</summary>
    [Parameter] public string? QualifiedName { get; set; }

    /// <summary>Route segment from <c>/docs/{{pkg@ver}}/search/{{Symbol}}</c>.</summary>
    [Parameter] public string? Symbol { get; set; }

    [Inject]
    private HttpClient Http { get; set; } = default!;

    private bool _parseError;
    private string _packageSegment = string.Empty;
    private string _versionSegment = string.Empty;
    private string _packageLabel = string.Empty;
    private string? _packagesHref;
    private bool _docsIndexLoading;
    private bool _useStructuredFullPage;
    private PackageDocsIndexResponse? _docsIndex;
    private string? _deepLinkQualifiedName;
    private string? _initialSymbolSearch;
    private bool _apiOrSearchWithoutStructured;
    private IReadOnlyList<string> _publishedVersions = [];
    private string PageHeading => _parseError ? "Documentation" : $"Docs · {_packageLabel}";

    protected override async Task OnParametersSetAsync()
    {
        _parseError = false;
        _packagesHref = null;
        _packageLabel = string.Empty;
        _docsIndex = null;
        _useStructuredFullPage = false;
        _deepLinkQualifiedName = null;
        _initialSymbolSearch = null;
        _apiOrSearchWithoutStructured = false;

        var raw = Uri.UnescapeDataString(PackageWithVersion ?? string.Empty).Trim().TrimEnd('/');
        var at = raw.IndexOf('@');
        if (at <= 0 || at >= raw.Length - 1)
        {
            _parseError = true;
            return;
        }

        _packageSegment = raw[..at].Trim();
        _versionSegment = raw[(at + 1)..].Trim();
        if (string.IsNullOrWhiteSpace(_packageSegment) || string.IsNullOrWhiteSpace(_versionSegment))
        {
            _parseError = true;
            return;
        }

        _packageLabel = _packageSegment;
        _publishedVersions = [];
        PackageEntity? packageRow = null;
        if (Guid.TryParse(_packageSegment, out var packageId))
        {
            packageRow = await DbContext.Packages.AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == packageId);
            if (packageRow is not null)
            {
                _packageLabel = packageRow.Name;
                _packagesHref = $"/packages/{Uri.EscapeDataString(packageRow.Name)}";
            }
        }
        else
        {
            _packagesHref = $"/packages/{Uri.EscapeDataString(_packageSegment)}";
            packageRow = await DbContext.Packages.AsNoTracking()
                .SingleOrDefaultAsync(x => x.Name == _packageSegment);
        }

        if (packageRow is not null)
        {
            _publishedVersions = await DbContext.PackageVersions.AsNoTracking()
                .Where(x => x.PackageId == packageRow.Id)
                .OrderByDescending(x => x.PublishedAtUtc)
                .Select(x => x.Version)
                .ToListAsync();
        }

        await LoadDocsIndexForViewAsync();

        var qn = string.IsNullOrWhiteSpace(QualifiedName) ? null : Uri.UnescapeDataString(QualifiedName).Trim();
        var sym = string.IsNullOrWhiteSpace(Symbol) ? null : Uri.UnescapeDataString(Symbol).Trim();
        _deepLinkQualifiedName = qn;
        _initialSymbolSearch = sym;

        var wantsStructuredRoute = _deepLinkQualifiedName is not null || _initialSymbolSearch is not null;
        if (wantsStructuredRoute && !_useStructuredFullPage && !_docsIndexLoading)
        {
            _apiOrSearchWithoutStructured = true;
        }
    }

    private async Task LoadDocsIndexForViewAsync()
    {
        _docsIndexLoading = true;
        try
        {
            var response = await Http.GetAsync(
                PackageDocumentationUrls.DocsIndex(_packageSegment.Trim(), _versionSegment.Trim()));
            if (!response.IsSuccessStatusCode)
            {
                return;
            }

            _docsIndex = await response.Content.ReadFromJsonAsync<PackageDocsIndexResponse>();
            _useStructuredFullPage = _docsIndex?.HasStructuredApiDoc == true;
        }
        finally
        {
            _docsIndexLoading = false;
        }
    }

    private Task OnDocsVersionChangedAsync(string? newVersion)
    {
        if (string.IsNullOrWhiteSpace(newVersion)
            || string.Equals(newVersion, _versionSegment, StringComparison.OrdinalIgnoreCase))
        {
            return Task.CompletedTask;
        }

        var target = !string.IsNullOrWhiteSpace(QualifiedName)
            ? AppDocumentationRoutes.AppDocsApiMember(
                _packageSegment,
                newVersion,
                Uri.UnescapeDataString(QualifiedName))
            : !string.IsNullOrWhiteSpace(Symbol)
                ? AppDocumentationRoutes.AppDocsSymbolSearch(
                    _packageSegment,
                    newVersion,
                    Uri.UnescapeDataString(Symbol))
                : AppDocumentationRoutes.AppDocsBase(_packageSegment, newVersion);

        Navigation.NavigateTo(target);
        return Task.CompletedTask;
    }
}

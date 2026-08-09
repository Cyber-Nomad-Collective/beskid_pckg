using System.Net;
using System.Text.Json;
using Server.Contracts.ApiDocumentation;
using Server.Services;

namespace Server.Components.Docs;

public partial class PackageDocs
{
    protected override async Task OnParametersSetAsync()
    {
        if (string.IsNullOrWhiteSpace(PackageIdentifier) || string.IsNullOrWhiteSpace(Version))
        {
            _loading = false;
            _loadError = "Package or version is missing.";
            return;
        }

        var key = $"{PackageIdentifier.Trim()}|{Version.Trim()}";
        var deep = string.IsNullOrWhiteSpace(DeepLinkQualifiedName)
            ? null
            : DeepLinkQualifiedName.Trim();
        var symInit = string.IsNullOrWhiteSpace(InitialSymbolSearch)
            ? null
            : InitialSymbolSearch.Trim();

        if (_fetchKey == key && _doc is not null)
        {
            if (
                !string.Equals(_appliedDeepLink, deep, StringComparison.Ordinal)
                || !string.Equals(_appliedInitialSymbol, symInit, StringComparison.Ordinal)
            )
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
        _symbolQuery = string.IsNullOrWhiteSpace(InitialSymbolSearch)
            ? string.Empty
            : InitialSymbolSearch.Trim();
        try
        {
            var url = PackageDocumentationUrls.DocsStructured(
                PackageIdentifier.Trim(),
                Version.Trim()
            );
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
            var doc = JsonSerializer.Deserialize<StructuredApiDocDto>(
                json,
                StructuredApiDocJson.Options
            );
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
            _navRoots = ApiDocNavigationBuilder.BuildLibraryTreeRoots(
                doc,
                PackageIdentifier.Trim()
            );
            _expandedNavIds.Clear();
            foreach (var root in _navRoots)
            {
                _expandedNavIds.Add(root.ExpansionKey);
            }

            _selected =
                FindDefaultSelection(doc)
                ?? doc.Items.Where(i => i.Id is not null).OrderBy(i => i.Id).FirstOrDefault();
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
                || string.Equals(x.QualifiedName, target, StringComparison.Ordinal)
            );
            if (match is not null)
            {
                _selected = match;
                EnsureExpandedForItem(match);
            }
            else
            {
                _deepLinkMissMessage = $"No API member matched the qualified name \"{target}\".";
                _selected ??= _doc
                    .Items.Where(i => i.Id is not null)
                    .OrderBy(i => i.Id)
                    .FirstOrDefault();
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
}

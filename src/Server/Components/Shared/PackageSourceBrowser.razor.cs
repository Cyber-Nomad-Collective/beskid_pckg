using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Components;
using Server.Features.Packages;
using Server.Services;

namespace Server.Components.Shared;

public partial class PackageSourceBrowser
{
    private sealed record TreeRow(PackageSourceTreeNodeResponse Node, int Depth);

    [Inject] private HttpClient Http { get; set; } = default!;

    [Parameter] public string PackageIdentifier { get; set; } = string.Empty;
    [Parameter] public string Version { get; set; } = "latest";

    /// <summary>When true, the Source tab is active and Monaco should relayout after paint.</summary>
    [Parameter] public bool TabActive { get; set; }

    private readonly List<PackageSourceTreeNodeResponse> _nodes = [];
    private string? _cachedPackage;
    private string? _cachedVersion;
    private string _search = string.Empty;
    private bool _isLoadingTree;
    private string? _treeError;

    private PackageSourceTreeNodeResponse? _selectedNode;
    private string? _selectedPath;
    private bool _isLoadingPreview;
    private string? _previewError;
    private string _previewKind = "none";
    private string _textContent = string.Empty;
    private string _monacoLanguage = "plaintext";
    private string? _imageUrl;
    private int _layoutGeneration;
    private bool _wasTabActive;

    private string PreviewPanelClass =>
        _selectedNode is null ? "source-preview-panel source-preview-panel--empty" : "source-preview-panel";

    protected override async Task OnParametersSetAsync()
    {
        var pkg = PackageIdentifier.Trim();
        var ver = Version.Trim();
        if (string.IsNullOrWhiteSpace(pkg) || string.IsNullOrWhiteSpace(ver))
        {
            _treeError = "Package or version is missing.";
            _isLoadingTree = false;
            return;
        }

        if (TabActive && !_wasTabActive)
        {
            _layoutGeneration++;
        }

        _wasTabActive = TabActive;

        if (string.Equals(_cachedPackage, pkg, StringComparison.Ordinal)
            && string.Equals(_cachedVersion, ver, StringComparison.Ordinal))
        {
            return;
        }

        _cachedPackage = pkg;
        _cachedVersion = ver;
        await LoadTreeAsync();
    }

    private async Task LoadTreeAsync()
    {
        _isLoadingTree = true;
        _treeError = null;
        _nodes.Clear();
        _selectedNode = null;
        _selectedPath = null;
        ResetPreview();

        try
        {
            var response = await Http.GetAsync(PackageDocumentationUrls.SourceTree(PackageIdentifier, Version));
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                _treeError =
                    "Source tree was not found for this package version. "
                    + "The version may not exist, you may lack access, or the published artifact file may be missing from registry storage.";
                return;
            }

            if (response.StatusCode == HttpStatusCode.InternalServerError)
            {
                _treeError =
                    "The published artifact for this version could not be read from storage (checksum or archive error). "
                    + "Republish the package or check the registry artifact volume on the server.";
                return;
            }

            if (!response.IsSuccessStatusCode)
            {
                _treeError = "Could not load source tree.";
                return;
            }

            var payload = await response.Content.ReadFromJsonAsync<PackageSourceTreeResponse>();
            if (payload?.Nodes is not { Count: > 0 })
            {
                _treeError = "No source files were found in this package version.";
                return;
            }

            _nodes.AddRange(payload.Nodes.OrderBy(x => x.Path, StringComparer.OrdinalIgnoreCase));
            var firstFile = _nodes.FirstOrDefault(x => !x.IsDirectory);
            if (firstFile is not null)
            {
                await SelectFileAsync(firstFile);
            }
        }
        catch
        {
            _treeError = "Could not load source tree (network or unexpected error).";
        }
        finally
        {
            _isLoadingTree = false;
        }
    }

    private async Task SelectFileAsync(PackageSourceTreeNodeResponse node)
    {
        if (node.IsDirectory)
        {
            return;
        }

        _selectedNode = node;
        _selectedPath = node.Path;
        await LoadPreviewAsync(node);
    }

    private async Task LoadPreviewAsync(PackageSourceTreeNodeResponse node)
    {
        _isLoadingPreview = true;
        _previewError = null;
        ResetPreview();
        _previewKind = node.PreviewKind ?? "none";
        _monacoLanguage = node.MonacoLanguage ?? "plaintext";

        try
        {
            var url = PackageDocumentationUrls.SourceFile(PackageIdentifier, Version, node.Path);
            if (string.Equals(node.PreviewKind, "image", StringComparison.OrdinalIgnoreCase))
            {
                // Let browser load image from endpoint directly.
                _imageUrl = url;
                _previewKind = "image";
                return;
            }

            if (!string.Equals(node.PreviewKind, "text", StringComparison.OrdinalIgnoreCase))
            {
                _previewKind = "none";
                return;
            }

            var response = await Http.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                _previewError = "Could not load selected file.";
                return;
            }

            _textContent = await response.Content.ReadAsStringAsync();
            if (response.Headers.TryGetValues("X-Beskid-Monaco-Language", out var values))
            {
                _monacoLanguage = values.FirstOrDefault() ?? _monacoLanguage;
            }

            _previewKind = "text";
            _layoutGeneration++;
        }
        catch
        {
            _previewError = "Could not load selected file.";
        }
        finally
        {
            _isLoadingPreview = false;
        }
    }

    private static string KindDisplayLabel(PackageSourceTreeNodeResponse node)
    {
        var raw = (node.FileType ?? node.IconKey ?? "file").Trim();
        if (raw.Length == 0)
        {
            return "File";
        }

        if (raw.Length <= 28)
        {
            return char.ToUpperInvariant(raw[0]) + raw[1..];
        }

        return $"{char.ToUpperInvariant(raw[0])}{raw[1..25]}…";
    }

    private IEnumerable<TreeRow> FilteredRows()
    {
        var filtered = string.IsNullOrWhiteSpace(_search)
            ? _nodes
            : _nodes.Where(x => x.Path.Contains(_search, StringComparison.OrdinalIgnoreCase) || x.Name.Contains(_search, StringComparison.OrdinalIgnoreCase)).ToList();

        foreach (var node in filtered)
        {
            yield return new TreeRow(node, node.Path.Count(ch => ch == '/'));
        }
    }

    private static string FormatSize(long bytes)
    {
        string[] suffixes = ["B", "KB", "MB", "GB", "TB"];
        if (bytes <= 0)
        {
            return "0 B";
        }

        var place = Convert.ToInt32(Math.Floor(Math.Log(bytes, 1024)));
        var num = Math.Round(bytes / Math.Pow(1024, place), 1);
        return $"{num} {suffixes[place]}";
    }

    private void ResetPreview()
    {
        _previewKind = "none";
        _textContent = string.Empty;
        _monacoLanguage = "plaintext";
        _imageUrl = null;
    }
}

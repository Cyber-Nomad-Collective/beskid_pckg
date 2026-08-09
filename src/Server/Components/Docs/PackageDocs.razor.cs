using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Server.Contracts.ApiDocumentation;

namespace Server.Components.Docs;

public partial class PackageDocs
{
    public enum PackageDocsVariant
    {
        Embedded,
        FullPage,
    }

    [Parameter]
    public string PackageIdentifier { get; set; } = string.Empty;

    [Parameter]
    public string Version { get; set; } = "latest";

    [Parameter]
    public string? DeepLinkQualifiedName { get; set; }

    [Parameter]
    public string? InitialSymbolSearch { get; set; }

    [Parameter]
    public PackageDocsVariant Variant { get; set; } = PackageDocsVariant.Embedded;

    [Inject]
    private HttpClient Http { get; set; } = default!;

    [Inject]
    private NavigationManager Navigation { get; set; } = default!;

    [Inject]
    private IJSRuntime Js { get; set; } = default!;

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
}

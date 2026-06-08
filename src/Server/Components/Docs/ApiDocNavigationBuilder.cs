using Server.Contracts.ApiDocumentation;

namespace Server.Components.Docs;

/// <summary>Builds Microsoft Docs–style library navigation from <c>api.json</c> graph fields.</summary>
public static class ApiDocNavigationBuilder
{
    public const string NavigationModelGraphV1 = "graph-v1";

    /// <summary>Compiler intrinsics (<c>beskid::…</c>) — omitted from dependency trees (same in every package).</summary>
    public const string BuiltinDeclaringPackage = "beskid";

    private static readonly string[] ExplorerKinds =
        ["module", "type", "enum", "contract", "function", "test"];

    private static readonly StringComparer KindComparer = StringComparer.OrdinalIgnoreCase;

    public static bool SupportsStructuredGraph(StructuredApiDocDto doc)
    {
        if (doc.SchemaVersion < 3)
        {
            return false;
        }

        if (!string.Equals(doc.NavigationModel, NavigationModelGraphV1, StringComparison.Ordinal))
        {
            return false;
        }

        return doc.Items.All(i => i.Id is not null);
    }

    /// <summary>Build package-grouped library tree roots (nested modules, kind folders, dependency groups).</summary>
    public static IReadOnlyList<GraphNavNode> BuildLibraryTreeRoots(
        StructuredApiDocDto doc,
        string publishingPackageId)
    {
        var publishing = publishingPackageId.Trim();
        var hasForeign = doc.Items.Any(i =>
            !string.IsNullOrWhiteSpace(i.DeclaringPackage)
            && !string.Equals(i.DeclaringPackage.Trim(), publishing, StringComparison.OrdinalIgnoreCase));

        if (!hasForeign)
        {
            return BuildModuleTreeForItems(doc.Items, publishing, startFolderId: -1);
        }

        var roots = new List<GraphNavNode>();
        var inPackage = doc.Items
            .Where(i => string.IsNullOrWhiteSpace(i.DeclaringPackage)
                || string.Equals(i.DeclaringPackage.Trim(), publishing, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (inPackage.Count > 0)
        {
            roots.AddRange(BuildModuleTreeForItems(inPackage, publishing, -1));
        }

        var depGroups = doc.Items
            .Where(i => !string.IsNullOrWhiteSpace(i.DeclaringPackage)
                && !string.Equals(i.DeclaringPackage!.Trim(), publishing, StringComparison.OrdinalIgnoreCase)
                && !IsHiddenDependencyPackage(i.DeclaringPackage))
            .GroupBy(i => i.DeclaringPackage!.Trim(), StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase);

        var folderId = -1000;
        var depChildren = new List<GraphNavNode>();
        foreach (var group in depGroups)
        {
            var subtree = BuildModuleTreeForItems(group.ToList(), group.Key, folderId);
            folderId -= subtree.Count + 8;
            depChildren.Add(GraphNavNode.PackageGroup(group.Key, subtree, ref folderId));
        }

        if (depChildren.Count > 0)
        {
            roots.Add(GraphNavNode.Folder("Dependencies", depChildren, ref folderId));
        }

        return roots;
    }

    /// <summary>Legacy flat graph roots (parentId only, no kind folders).</summary>
    public static IReadOnlyList<GraphNavNode> BuildGraphRoots(StructuredApiDocDto doc) =>
        BuildLibraryTreeRoots(doc, publishingPackageId: string.Empty);

    public static IEnumerable<StructuredApiItemDto> ModuleScopeRootCandidates(StructuredApiDocDto doc) =>
        doc.Items.Where(i =>
            string.Equals(i.Kind, "module", StringComparison.OrdinalIgnoreCase)
            && (i.ParentId is null || IsTopLevelModule(i, doc)));

    private static bool IsTopLevelModule(StructuredApiItemDto item, StructuredApiDocDto doc)
    {
        if (item.ParentId is not int pid)
        {
            return true;
        }

        var parent = doc.Items.FirstOrDefault(x => x.Id == pid);
        return parent is null || !string.Equals(parent.Kind, "module", StringComparison.OrdinalIgnoreCase);
    }

    public static IReadOnlyList<GraphNavNode> FilterGraphRoots(
        IReadOnlyList<GraphNavNode> roots,
        IReadOnlySet<int> visibleIds)
    {
        if (visibleIds.Count == 0)
        {
            return [];
        }

        var pruned = new List<GraphNavNode>();
        foreach (var root in roots)
        {
            var filtered = FilterNode(root, visibleIds);
            if (filtered is not null)
            {
                pruned.Add(filtered);
            }
        }

        return pruned;
    }

    private static IReadOnlyList<GraphNavNode> BuildModuleTreeForItems(
        List<StructuredApiItemDto> items,
        string packageLabel,
        int startFolderId)
    {
        var folderId = startFolderId;
        var childBuckets = BuildChildBuckets(items);
        var roots = items
            .Where(i => i.ParentId is null && IsNavExplorerKind(i.Kind))
            .OrderBy(i => i.QualifiedName ?? i.Name, StringComparer.Ordinal)
            .ToList();

        if (roots.Count == 0)
        {
            roots = items
                .Where(i => i.ParentId is null)
                .OrderBy(i => i.QualifiedName ?? i.Name, StringComparer.Ordinal)
                .ToList();
        }

        if (NeedsLegacyModuleSynthesis(roots, items))
        {
            return SynthesizeLegacyModuleTree(items, ref folderId);
        }

        return roots
            .Select(r => BuildSymbolNode(r, childBuckets, ref folderId))
            .ToList();
    }

    private static bool NeedsLegacyModuleSynthesis(
        IReadOnlyList<StructuredApiItemDto> roots,
        List<StructuredApiItemDto> items)
    {
        if (roots.Count > 64)
        {
            return true;
        }

        var moduleRoots = items.Count(i =>
            i.ParentId is null && string.Equals(i.Kind, "module", StringComparison.OrdinalIgnoreCase));
        return moduleRoots > 0
            && items.Any(i =>
                i.ParentId is null
                && !string.Equals(i.Kind, "module", StringComparison.OrdinalIgnoreCase)
                && i.ModulePath.Count > 0);
    }

    private static List<GraphNavNode> SynthesizeLegacyModuleTree(
        List<StructuredApiItemDto> items,
        ref int folderId)
    {
        var byPath = new Dictionary<string, List<StructuredApiItemDto>>(StringComparer.Ordinal);
        foreach (var item in items.Where(i =>
                     IsNavExplorerKind(i.Kind)
                     || string.Equals(i.Kind, "module", StringComparison.OrdinalIgnoreCase)))
        {
            var path = item.ModulePath.Count > 0
                ? item.ModulePath
                : QualifiedModulePath(item.QualifiedName);
            if (string.Equals(item.Kind, "module", StringComparison.OrdinalIgnoreCase))
            {
                var fromQn = QualifiedModulePath(item.QualifiedName);
                if (fromQn.Count >= path.Count)
                {
                    path = fromQn;
                }
            }

            var key = string.Join("\0", path);
            if (!byPath.TryGetValue(key, out var list))
            {
                list = [];
                byPath[key] = list;
            }

            list.Add(item);
        }

        var pathNodes = new Dictionary<string, GraphNavNode>(StringComparer.Ordinal);
        foreach (var key in byPath.Keys.OrderBy(k => k, StringComparer.Ordinal))
        {
            var segments = key.Length == 0
                ? Array.Empty<string>()
                : key.Split('\0', StringSplitOptions.RemoveEmptyEntries);
            var acc = new List<string>();
            GraphNavNode? leaf = null;
            foreach (var seg in segments)
            {
                acc.Add(seg);
                var accKey = string.Join("\0", acc);
                if (!pathNodes.TryGetValue(accKey, out var node))
                {
                    var moduleItem = byPath[key]
                        .FirstOrDefault(i =>
                            string.Equals(i.Kind, "module", StringComparison.OrdinalIgnoreCase)
                            && string.Equals(
                                i.QualifiedName ?? i.Name,
                                string.Join("::", acc),
                                StringComparison.Ordinal));
                    node = moduleItem is not null
                        ? GraphNavNode.Symbol(moduleItem)
                        : GraphNavNode.Folder(seg, [], ref folderId);
                    pathNodes[accKey] = node;
                    if (acc.Count > 1)
                    {
                        var parentKey = string.Join("\0", acc[..^1]);
                        if (pathNodes.TryGetValue(parentKey, out var parent))
                        {
                            if (!parent.Children.Any(c =>
                                    c.Item?.Id == node.Item?.Id
                                    && c.Label == node.Label))
                            {
                                parent.Children.Add(node);
                            }
                        }
                    }
                }

                leaf = node;
            }

            if (leaf is null)
            {
                continue;
            }

            foreach (var member in byPath[key]
                         .Where(i => !string.Equals(i.Kind, "module", StringComparison.OrdinalIgnoreCase))
                         .OrderBy(i => i.QualifiedName ?? i.Name, StringComparer.Ordinal))
            {
                leaf!.Children.Add(GraphNavNode.Symbol(member));
            }
        }

        return pathNodes
            .Where(kv => !kv.Key.Contains('\0', StringComparison.Ordinal))
            .Select(kv => kv.Value)
            .OrderBy(n => n.Label, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string ModulePathKey(StructuredApiItemDto item) =>
        string.Join("\0", item.ModulePath.Count > 0 ? item.ModulePath : QualifiedModulePath(item.QualifiedName));

    private static List<string> QualifiedModulePath(string? qualifiedName)
    {
        if (string.IsNullOrWhiteSpace(qualifiedName))
        {
            return [];
        }

        var segments = qualifiedName.Split("::", StringSplitOptions.RemoveEmptyEntries);
        return segments.Length <= 1 ? segments.ToList() : segments[..^1].ToList();
    }

    private static Dictionary<int, List<StructuredApiItemDto>> BuildChildBuckets(List<StructuredApiItemDto> items)
    {
        var childBuckets = new Dictionary<int, List<StructuredApiItemDto>>();
        foreach (var it in items)
        {
            if (it.ParentId is int pid)
            {
                if (!childBuckets.TryGetValue(pid, out var list))
                {
                    list = [];
                    childBuckets[pid] = list;
                }

                list.Add(it);
            }
        }

        foreach (var (_, list) in childBuckets)
        {
            list.Sort((a, b) => string.Compare(
                a.QualifiedName ?? a.Name,
                b.QualifiedName ?? b.Name,
                StringComparison.Ordinal));
        }

        foreach (var parent in items)
        {
            if (parent.Id is not int pid || parent.MemberIds.Count == 0)
            {
                continue;
            }

            if (!childBuckets.TryGetValue(pid, out var bucket))
            {
                continue;
            }

            var ordered = new List<StructuredApiItemDto>();
            var seen = new HashSet<int>();
            foreach (var mid in parent.MemberIds)
            {
                var ch = bucket.FirstOrDefault(x => x.Id == mid);
                if (ch?.Id is int id && seen.Add(id))
                {
                    ordered.Add(ch);
                }
            }

            foreach (var ch in bucket)
            {
                if (ch.Id is int id && seen.Add(id))
                {
                    ordered.Add(ch);
                }
            }

            childBuckets[pid] = ordered;
        }

        return childBuckets;
    }

    private static GraphNavNode BuildSymbolNode(
        StructuredApiItemDto item,
        IReadOnlyDictionary<int, List<StructuredApiItemDto>> childBuckets,
        ref int folderId)
    {
        var node = GraphNavNode.Symbol(item);
        if (item.Id is not int id || !childBuckets.TryGetValue(id, out var kids))
        {
            return node;
        }

        foreach (var k in kids.Where(k => IsNavExplorerKind(k.Kind)))
        {
            node.Children.Add(BuildSymbolNode(k, childBuckets, ref folderId));
        }

        return node;
    }

    private static bool IsHiddenDependencyPackage(string? declaringPackage) =>
        !string.IsNullOrWhiteSpace(declaringPackage)
        && string.Equals(declaringPackage.Trim(), BuiltinDeclaringPackage, StringComparison.OrdinalIgnoreCase);

    private static bool IsNavExplorerKind(string? kind) =>
        !string.IsNullOrWhiteSpace(kind) && ExplorerKinds.Contains(kind, KindComparer);

    private static GraphNavNode? FilterNode(GraphNavNode node, IReadOnlySet<int> visibleIds)
    {
        var children = new List<GraphNavNode>();
        foreach (var child in node.Children)
        {
            var filteredChild = FilterNode(child, visibleIds);
            if (filteredChild is not null)
            {
                children.Add(filteredChild);
            }
        }

        if (node.Item?.Id is int id)
        {
            if (!visibleIds.Contains(id) && children.Count == 0)
            {
                return null;
            }

            var result = GraphNavNode.Symbol(node.Item);
            result.Children.AddRange(children);
            return result;
        }

        if (children.Count == 0)
        {
            return null;
        }

        return node.Role switch
        {
            GraphNavNodeRole.Folder => GraphNavNode.Folder(node.Label, children, node.ExpansionKey),
            GraphNavNodeRole.PackageGroup => GraphNavNode.PackageGroup(node.Label, children, node.ExpansionKey),
            _ => null,
        };
    }
}

public enum GraphNavNodeRole
{
    Symbol,
    Folder,
    PackageGroup,
}

/// <summary>One node in the library tree (API symbol, kind folder, or dependency package group).</summary>
public sealed class GraphNavNode
{
    public GraphNavNodeRole Role { get; }
    public StructuredApiItemDto? Item { get; }
    public string Label { get; }
    public int ExpansionKey { get; }
    public List<GraphNavNode> Children { get; } = [];

    private GraphNavNode(GraphNavNodeRole role, StructuredApiItemDto? item, string label, int expansionKey)
    {
        Role = role;
        Item = item;
        Label = label;
        ExpansionKey = expansionKey;
    }

    public static GraphNavNode Symbol(StructuredApiItemDto item) =>
        new(GraphNavNodeRole.Symbol, item, item.DisplayName ?? item.Name ?? item.QualifiedName ?? "?", item.Id ?? 0);

    public static GraphNavNode Folder(string label, IReadOnlyList<GraphNavNode> children, ref int folderId)
    {
        var node = new GraphNavNode(GraphNavNodeRole.Folder, null, label, folderId--);
        node.Children.AddRange(children);
        return node;
    }

    public static GraphNavNode Folder(string label, IReadOnlyList<GraphNavNode> children, int expansionKey)
    {
        var node = new GraphNavNode(GraphNavNodeRole.Folder, null, label, expansionKey);
        node.Children.AddRange(children);
        return node;
    }

    public static GraphNavNode PackageGroup(string packageId, IReadOnlyList<GraphNavNode> children, ref int folderId)
    {
        var node = new GraphNavNode(GraphNavNodeRole.PackageGroup, null, packageId, folderId--);
        node.Children.AddRange(children);
        return node;
    }

    public static GraphNavNode PackageGroup(
        string packageId,
        IReadOnlyList<GraphNavNode> children,
        int expansionKey)
    {
        var node = new GraphNavNode(GraphNavNodeRole.PackageGroup, null, packageId, expansionKey);
        node.Children.AddRange(children);
        return node;
    }
}

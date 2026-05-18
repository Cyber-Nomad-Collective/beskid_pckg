using Server.Contracts.ApiDocumentation;

namespace Server.Components.Docs;

/// <summary>Builds API navigation strictly from <c>api.json</c> graph fields (no <c>qualifiedName</c> splitting).</summary>
public static class ApiDocNavigationBuilder
{
    public const string NavigationModelGraphV1 = "graph-v1";

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

        foreach (var it in doc.Items)
        {
            if (it.Id is null)
            {
                return false;
            }
        }

        return true;
    }

    public static IReadOnlyList<GraphNavNode> BuildGraphRoots(StructuredApiDocDto doc)
    {
        var byId = doc.Items.Where(i => i.Id is not null).ToDictionary(i => i.Id!.Value);
        var childBuckets = new Dictionary<int, List<StructuredApiItemDto>>();
        foreach (var it in doc.Items)
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
            list.Sort((a, b) => (a.Id ?? 0).CompareTo(b.Id ?? 0));
        }

        foreach (var parent in doc.Items)
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
                if (ch is null || ch.Id is null)
                {
                    continue;
                }

                if (seen.Add(ch.Id.Value))
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

        var roots = doc.Items.Where(i => i.ParentId is null).OrderBy(i => i.Id ?? 0).ToList();
        return roots.Select(r => BuildNode(r, childBuckets)).ToList();
    }

    private static GraphNavNode BuildNode(
        StructuredApiItemDto item,
        IReadOnlyDictionary<int, List<StructuredApiItemDto>> childBuckets)
    {
        var node = new GraphNavNode(item);
        if (item.Id is not int id || !childBuckets.TryGetValue(id, out var kids))
        {
            return node;
        }

        foreach (var k in kids)
        {
            node.Children.Add(BuildNode(k, childBuckets));
        }

        return node;
    }

    public static IEnumerable<StructuredApiItemDto> ModuleScopeRootCandidates(StructuredApiDocDto doc) =>
        doc.Items.Where(i =>
            i.ParentId is null
            && string.Equals(i.Kind, "module", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Prunes <paramref name="roots"/> to nodes whose <see cref="StructuredApiItemDto.Id"/> is in
    /// <paramref name="visibleIds"/> (typically matches plus ancestors for context).
    /// </summary>
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

    private static GraphNavNode? FilterNode(GraphNavNode node, IReadOnlySet<int> visibleIds)
    {
        if (node.Item.Id is not int id)
        {
            return null;
        }

        var children = new List<GraphNavNode>();
        foreach (var child in node.Children)
        {
            var filteredChild = FilterNode(child, visibleIds);
            if (filteredChild is not null)
            {
                children.Add(filteredChild);
            }
        }

        if (!visibleIds.Contains(id) && children.Count == 0)
        {
            return null;
        }

        var result = new GraphNavNode(node.Item);
        result.Children.AddRange(children);
        return result;
    }
}

/// <summary>One row in the API graph with resolved children.</summary>
public sealed class GraphNavNode(StructuredApiItemDto item)
{
    public StructuredApiItemDto Item { get; } = item;

    public List<GraphNavNode> Children { get; } = [];
}

using Server.Contracts.ApiDocumentation;
using Server.Services;

namespace Server.Components.Docs;

public partial class PackageDocs
{
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
        out List<int> expansionKeys
    )
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
        out List<int> expansionKeys
    )
    {
        prefix.Add(node.ExpansionKey);
        if (node.Item?.Id == targetItemId)
        {
            expansionKeys = prefix;
            return true;
        }

        foreach (var child in node.Children)
        {
            if (TryFindNavPathCore(child, targetItemId, [.. prefix], out expansionKeys))
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
            && i.ParentId is null
        );
        if (module is not null)
        {
            return module;
        }

        return doc.Items.FirstOrDefault(i =>
            i.ParentId is null
            && (
                string.Equals(i.Kind, "type", StringComparison.OrdinalIgnoreCase)
                || string.Equals(i.Kind, "enum", StringComparison.OrdinalIgnoreCase)
                || string.Equals(i.Kind, "contract", StringComparison.OrdinalIgnoreCase)
            )
        );
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
            qn
        );
        var current = Navigation.ToBaseRelativePath(Navigation.Uri);
        if (string.Equals(current, target.TrimStart('/'), StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        Navigation.NavigateTo(target, replace: true);
    }
}

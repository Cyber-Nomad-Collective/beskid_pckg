using Microsoft.FluentUI.AspNetCore.Components;
using Icons = Microsoft.FluentUI.AspNetCore.Components.Icons;

namespace Server.Components.Docs;

public static class ApiDocTreeIcons
{
    public static Icon? ForNode(GraphNavNode node)
    {
        if (node.Item is not null)
        {
            return null;
        }

        return node.Role switch
        {
            GraphNavNodeRole.PackageGroup => new Icons.Regular.Size16.Box(),
            GraphNavNodeRole.Folder => new Icons.Regular.Size16.Folder(),
            _ => new Icons.Regular.Size16.Folder(),
        };
    }
}

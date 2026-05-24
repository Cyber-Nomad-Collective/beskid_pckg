using Microsoft.FluentUI.AspNetCore.Components;

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
            GraphNavNodeRole.PackageGroup => new Microsoft.FluentUI.AspNetCore.Components.Icons.Regular.Size16.Box(),
            GraphNavNodeRole.Folder => new Microsoft.FluentUI.AspNetCore.Components.Icons.Regular.Size16.Folder(),
            _ => new Microsoft.FluentUI.AspNetCore.Components.Icons.Regular.Size16.Folder(),
        };
    }
}

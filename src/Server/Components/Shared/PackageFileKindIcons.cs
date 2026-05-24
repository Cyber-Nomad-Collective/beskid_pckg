using Microsoft.FluentUI.AspNetCore.Components;

namespace Server.Components.Shared;

public static class PackageFileKindIcons
{
    public static Icon ForPreview(string? iconKey) => ForKey(iconKey, large: true);

    public static Icon ForTree(string? iconKey, bool isDirectory)
    {
        if (isDirectory)
        {
            return new Microsoft.FluentUI.AspNetCore.Components.Icons.Regular.Size16.Folder();
        }

        return ForKey(iconKey, large: false);
    }

    public static Icon ForKey(string? iconKey, bool large)
    {
        return (iconKey ?? string.Empty).ToLowerInvariant() switch
        {
            "markdown" => Pick(large, () => new Microsoft.FluentUI.AspNetCore.Components.Icons.Regular.Size20.DocumentText(), () => new Microsoft.FluentUI.AspNetCore.Components.Icons.Regular.Size16.DocumentText()),
            "json" => Pick(large, () => new Microsoft.FluentUI.AspNetCore.Components.Icons.Regular.Size20.DataBarHorizontal(), () => new Microsoft.FluentUI.AspNetCore.Components.Icons.Regular.Size16.Code()),
            "yaml" or "toml" or "xml" => Pick(large, () => new Microsoft.FluentUI.AspNetCore.Components.Icons.Regular.Size20.DocumentBulletList(), () => new Microsoft.FluentUI.AspNetCore.Components.Icons.Regular.Size16.DocumentBulletList()),
            "beskid" => Pick(large, () => new Microsoft.FluentUI.AspNetCore.Components.Icons.Regular.Size20.Library(), () => new Microsoft.FluentUI.AspNetCore.Components.Icons.Regular.Size16.Library()),
            "image" => Pick(large, () => new Microsoft.FluentUI.AspNetCore.Components.Icons.Regular.Size20.Image(), () => new Microsoft.FluentUI.AspNetCore.Components.Icons.Regular.Size16.Image()),
            "project" => Pick(large, () => new Microsoft.FluentUI.AspNetCore.Components.Icons.Regular.Size20.Settings(), () => new Microsoft.FluentUI.AspNetCore.Components.Icons.Regular.Size16.Settings()),
            "binary" => Pick(large, () => new Microsoft.FluentUI.AspNetCore.Components.Icons.Regular.Size20.Box(), () => new Microsoft.FluentUI.AspNetCore.Components.Icons.Regular.Size16.Box()),
            "code" => Pick(large, () => new Microsoft.FluentUI.AspNetCore.Components.Icons.Regular.Size20.Code(), () => new Microsoft.FluentUI.AspNetCore.Components.Icons.Regular.Size16.Code()),
            "text" => Pick(large, () => new Microsoft.FluentUI.AspNetCore.Components.Icons.Regular.Size20.DocumentText(), () => new Microsoft.FluentUI.AspNetCore.Components.Icons.Regular.Size16.DocumentText()),
            _ => Pick(large, () => new Microsoft.FluentUI.AspNetCore.Components.Icons.Regular.Size20.Document(), () => new Microsoft.FluentUI.AspNetCore.Components.Icons.Regular.Size16.Document()),
        };
    }

    private static Icon Pick(bool large, Func<Icon> size20, Func<Icon> size16)
        => large ? size20() : size16();
}

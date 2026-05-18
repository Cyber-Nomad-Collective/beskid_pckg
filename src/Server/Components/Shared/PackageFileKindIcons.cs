using Microsoft.FluentUI.AspNetCore.Components;
using Icons = Microsoft.FluentUI.AspNetCore.Components.Icons;

namespace Server.Components.Shared;

public static class PackageFileKindIcons
{
    public static Icon ForPreview(string? iconKey) => ForKey(iconKey, large: true);

    public static Icon ForTree(string? iconKey, bool isDirectory)
    {
        if (isDirectory)
        {
            return new Icons.Regular.Size16.Folder();
        }

        return ForKey(iconKey, large: false);
    }

    public static Icon ForKey(string? iconKey, bool large)
    {
        return (iconKey ?? string.Empty).ToLowerInvariant() switch
        {
            "markdown" => Pick(large, () => new Icons.Regular.Size20.DocumentText(), () => new Icons.Regular.Size16.DocumentText()),
            "json" => Pick(large, () => new Icons.Regular.Size20.DataBarHorizontal(), () => new Icons.Regular.Size16.Code()),
            "yaml" or "toml" or "xml" => Pick(large, () => new Icons.Regular.Size20.DocumentBulletList(), () => new Icons.Regular.Size16.DocumentBulletList()),
            "beskid" => Pick(large, () => new Icons.Regular.Size20.Library(), () => new Icons.Regular.Size16.Library()),
            "image" => Pick(large, () => new Icons.Regular.Size20.Image(), () => new Icons.Regular.Size16.Image()),
            "project" => Pick(large, () => new Icons.Regular.Size20.Settings(), () => new Icons.Regular.Size16.Settings()),
            "binary" => Pick(large, () => new Icons.Regular.Size20.Box(), () => new Icons.Regular.Size16.Box()),
            "code" => Pick(large, () => new Icons.Regular.Size20.Code(), () => new Icons.Regular.Size16.Code()),
            "text" => Pick(large, () => new Icons.Regular.Size20.DocumentText(), () => new Icons.Regular.Size16.DocumentText()),
            _ => Pick(large, () => new Icons.Regular.Size20.Document(), () => new Icons.Regular.Size16.Document()),
        };
    }

    private static Icon Pick(bool large, Func<Icon> size20, Func<Icon> size16)
        => large ? size20() : size16();
}

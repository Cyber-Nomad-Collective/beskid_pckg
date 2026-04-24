using System.Text;

namespace Server.Services;

public enum PackageSourcePreviewKind
{
    None,
    Text,
    Image
}

public sealed record PackageSourceFileTypeInfo(
    string Kind,
    string IconKey,
    string? ContentType,
    PackageSourcePreviewKind PreviewKind,
    string? MonacoLanguage,
    bool IsText);

public interface IPackageSourceFileTypeMapper
{
    PackageSourceFileTypeInfo FromPath(string normalizedPath);
    PackageSourceFileTypeInfo FromPathAndBytes(string normalizedPath, byte[] bytes);
}

public sealed class PackageSourceFileTypeMapper : IPackageSourceFileTypeMapper
{
    public PackageSourceFileTypeInfo FromPath(string normalizedPath)
    {
        var ext = Path.GetExtension(normalizedPath).ToLowerInvariant();

        if (TryMapBeskid(ext, out var beskid))
        {
            return beskid;
        }

        if (TryMapImage(ext, out var image))
        {
            return image;
        }

        if (TryMapDataText(ext, out var dataText))
        {
            return dataText;
        }

        if (TryMapCodeText(ext, out var codeText))
        {
            return codeText;
        }

        if (TryMapBinary(ext, out var binary))
        {
            return binary;
        }

        // Generic fallback: treat unknown extensions as plain text first.
        return new PackageSourceFileTypeInfo(
            "text",
            "text",
            "text/plain; charset=utf-8",
            PackageSourcePreviewKind.Text,
            "plaintext",
            true);
    }

    public PackageSourceFileTypeInfo FromPathAndBytes(string normalizedPath, byte[] bytes)
    {
        var mapped = FromPath(normalizedPath);
        if (mapped.PreviewKind == PackageSourcePreviewKind.None && LooksLikeText(bytes))
        {
            return new PackageSourceFileTypeInfo(
                "text",
                "text",
                "text/plain; charset=utf-8",
                PackageSourcePreviewKind.Text,
                "plaintext",
                true);
        }

        return mapped;
    }

    private static bool TryMapBeskid(string ext, out PackageSourceFileTypeInfo info)
    {
        if (ext is ".bd")
        {
            info = new PackageSourceFileTypeInfo(
                "beskid",
                "beskid",
                "text/plain; charset=utf-8",
                PackageSourcePreviewKind.Text,
                "rust",
                true);
            return true;
        }

        if (ext is ".proj")
        {
            info = new PackageSourceFileTypeInfo(
                "project",
                "project",
                "text/plain; charset=utf-8",
                PackageSourcePreviewKind.Text,
                "ini",
                true);
            return true;
        }

        info = default!;
        return false;
    }

    private static bool TryMapImage(string ext, out PackageSourceFileTypeInfo info)
    {
        var isImage = ext is ".png" or ".jpg" or ".jpeg" or ".gif" or ".webp" or ".svg" or ".bmp";
        if (!isImage)
        {
            info = default!;
            return false;
        }

        var contentType = ext switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".svg" => "image/svg+xml",
            ".bmp" => "image/bmp",
            _ => "application/octet-stream"
        };

        info = new PackageSourceFileTypeInfo(
            "image",
            "image",
            contentType,
            PackageSourcePreviewKind.Image,
            null,
            false);
        return true;
    }

    private static bool TryMapDataText(string ext, out PackageSourceFileTypeInfo info)
    {
        switch (ext)
        {
            case ".json":
                info = new PackageSourceFileTypeInfo("json", "json", "application/json; charset=utf-8", PackageSourcePreviewKind.Text, "json", true);
                return true;
            case ".yaml":
            case ".yml":
                info = new PackageSourceFileTypeInfo("yaml", "yaml", "text/yaml; charset=utf-8", PackageSourcePreviewKind.Text, "yaml", true);
                return true;
            case ".toml":
                info = new PackageSourceFileTypeInfo("toml", "toml", "text/plain; charset=utf-8", PackageSourcePreviewKind.Text, "ini", true);
                return true;
            case ".xml":
                info = new PackageSourceFileTypeInfo("xml", "xml", "application/xml; charset=utf-8", PackageSourcePreviewKind.Text, "xml", true);
                return true;
            case ".md":
                info = new PackageSourceFileTypeInfo("markdown", "markdown", "text/markdown; charset=utf-8", PackageSourcePreviewKind.Text, "markdown", true);
                return true;
            default:
                info = default!;
                return false;
        }
    }

    private static bool TryMapCodeText(string ext, out PackageSourceFileTypeInfo info)
    {
        switch (ext)
        {
            case ".cs":
                info = new PackageSourceFileTypeInfo("csharp", "code", "text/plain; charset=utf-8", PackageSourcePreviewKind.Text, "csharp", true);
                return true;
            case ".ts":
                info = new PackageSourceFileTypeInfo("typescript", "code", "text/plain; charset=utf-8", PackageSourcePreviewKind.Text, "typescript", true);
                return true;
            case ".js":
                info = new PackageSourceFileTypeInfo("javascript", "code", "text/plain; charset=utf-8", PackageSourcePreviewKind.Text, "javascript", true);
                return true;
            case ".rs":
                info = new PackageSourceFileTypeInfo("rust", "code", "text/plain; charset=utf-8", PackageSourcePreviewKind.Text, "rust", true);
                return true;
            case ".py":
                info = new PackageSourceFileTypeInfo("python", "code", "text/plain; charset=utf-8", PackageSourcePreviewKind.Text, "python", true);
                return true;
            case ".sh":
                info = new PackageSourceFileTypeInfo("shell", "code", "text/plain; charset=utf-8", PackageSourcePreviewKind.Text, "shell", true);
                return true;
            case ".txt":
                info = new PackageSourceFileTypeInfo("text", "text", "text/plain; charset=utf-8", PackageSourcePreviewKind.Text, "plaintext", true);
                return true;
            default:
                info = default!;
                return false;
        }
    }

    private static bool TryMapBinary(string ext, out PackageSourceFileTypeInfo info)
    {
        var isBinary = ext is ".zip" or ".bpk" or ".pdf" or ".woff" or ".woff2" or ".ttf" or ".eot" or ".bin" or ".exe";
        if (!isBinary)
        {
            info = default!;
            return false;
        }

        info = new PackageSourceFileTypeInfo(
            "binary",
            "binary",
            "application/octet-stream",
            PackageSourcePreviewKind.None,
            null,
            false);
        return true;
    }

    private static bool LooksLikeText(byte[] bytes)
    {
        if (bytes.Length == 0)
        {
            return true;
        }

        // Quick binary heuristic: NUL almost always means binary.
        if (bytes.Contains((byte)0))
        {
            return false;
        }

        try
        {
            _ = Encoding.UTF8.GetString(bytes);
            return true;
        }
        catch
        {
            return false;
        }
    }
}

using System.IO.Compression;
using System.Text;
using Server.Features.Packages;

namespace Server.Services;

public interface IPackageSourceArchiveService
{
    Task<PackageSourceTreeResult> ListTreeAsync(
        HttpContext httpContext,
        string idOrName,
        string versionOrLatest,
        CancellationToken cancellationToken = default);

    Task<PackageSourceFileResult> ReadFileAsync(
        HttpContext httpContext,
        string idOrName,
        string versionOrLatest,
        string relativePath,
        CancellationToken cancellationToken = default);
}

public sealed class PackageSourceTreeResult(int statusCode, IReadOnlyList<PackageSourceTreeNodeResponse>? nodes = null)
{
    public int StatusCode { get; } = statusCode;
    public IReadOnlyList<PackageSourceTreeNodeResponse>? Nodes { get; } = nodes;
}

public sealed class PackageSourceFileResult(
    int statusCode,
    string? contentType = null,
    string? text = null,
    byte[]? bytes = null,
    PackageSourcePreviewKind previewKind = PackageSourcePreviewKind.None,
    string? monacoLanguage = null,
    string? fileTypeKind = null)
{
    public int StatusCode { get; } = statusCode;
    public string? ContentType { get; } = contentType;
    public string? Text { get; } = text;
    public byte[]? Bytes { get; } = bytes;
    public PackageSourcePreviewKind PreviewKind { get; } = previewKind;
    public string? MonacoLanguage { get; } = monacoLanguage;
    public string? FileTypeKind { get; } = fileTypeKind;
}

public sealed class PackageSourceArchiveService(
    IPackageArtifactExplorerService artifactExplorer,
    IPackageSourceFileTypeMapper fileTypeMapper) : IPackageSourceArchiveService
{
    public const int MaxSourceFileBytes = 1024 * 1024;
    public const int MaxTreeEntries = 4000;

    public async Task<PackageSourceTreeResult> ListTreeAsync(
        HttpContext httpContext,
        string idOrName,
        string versionOrLatest,
        CancellationToken cancellationToken = default)
    {
        var resolved = await artifactExplorer.ResolveVersionAsync(httpContext, idOrName, versionOrLatest, cancellationToken);
        if (!resolved.IsSuccess)
        {
            return new PackageSourceTreeResult(resolved.StatusCode);
        }

        var openResult = await artifactExplorer.OpenVerifiedArchiveAsync(resolved.Version!, cancellationToken);
        if (openResult.StatusCode != StatusCodes.Status200OK || openResult.Stream is null)
        {
            return new PackageSourceTreeResult(openResult.StatusCode);
        }

        using var memory = openResult.Stream;
        using var zip = new ZipArchive(memory, ZipArchiveMode.Read, leaveOpen: false);
        var entries = new List<PackageSourceTreeNodeResponse>();
        var addedDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in zip.Entries)
        {
            var normalizedPath = NormalizeEntryPath(entry.FullName);
            if (string.IsNullOrWhiteSpace(normalizedPath))
            {
                continue;
            }

            if (!PackageDocsPaths.HasOnlySafePathSegments(normalizedPath))
            {
                continue;
            }

            if (entry.FullName.EndsWith("/", StringComparison.Ordinal))
            {
                EnsureDirectories(normalizedPath.TrimEnd('/'), addedDirectories, entries);
                continue;
            }

            EnsureDirectories(normalizedPath, addedDirectories, entries);

            var info = fileTypeMapper.FromPath(normalizedPath);
            entries.Add(
                new PackageSourceTreeNodeResponse(
                    normalizedPath,
                    NameFromPath(normalizedPath),
                    false,
                    ParentPath(normalizedPath),
                    entry.Length,
                    info.Kind,
                    info.IconKey,
                    info.PreviewKind.ToString().ToLowerInvariant(),
                    info.MonacoLanguage,
                    info.ContentType));

            if (entries.Count >= MaxTreeEntries)
            {
                break;
            }
        }

        var ordered = entries
            .DistinctBy(x => x.Path, StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x.Path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new PackageSourceTreeResult(StatusCodes.Status200OK, ordered);
    }

    public async Task<PackageSourceFileResult> ReadFileAsync(
        HttpContext httpContext,
        string idOrName,
        string versionOrLatest,
        string relativePath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return new PackageSourceFileResult(StatusCodes.Status400BadRequest);
        }

        var normalizedRequested = NormalizeEntryPath(relativePath);
        if (!PackageDocsPaths.HasOnlySafePathSegments(normalizedRequested))
        {
            return new PackageSourceFileResult(StatusCodes.Status400BadRequest);
        }

        var resolved = await artifactExplorer.ResolveVersionAsync(httpContext, idOrName, versionOrLatest, cancellationToken);
        if (!resolved.IsSuccess)
        {
            return new PackageSourceFileResult(resolved.StatusCode);
        }

        var openResult = await artifactExplorer.OpenVerifiedArchiveAsync(resolved.Version!, cancellationToken);
        if (openResult.StatusCode != StatusCodes.Status200OK || openResult.Stream is null)
        {
            return new PackageSourceFileResult(openResult.StatusCode);
        }

        using var memory = openResult.Stream;
        using var zip = new ZipArchive(memory, ZipArchiveMode.Read, leaveOpen: false);

        var entry = zip.GetEntry(normalizedRequested)
                    ?? zip.Entries.FirstOrDefault(e =>
                        string.Equals(NormalizeEntryPath(e.FullName), normalizedRequested, StringComparison.OrdinalIgnoreCase));

        if (entry is null || string.IsNullOrEmpty(entry.Name))
        {
            return new PackageSourceFileResult(StatusCodes.Status404NotFound);
        }

        if (entry.Length > MaxSourceFileBytes)
        {
            return new PackageSourceFileResult(StatusCodes.Status413PayloadTooLarge);
        }

        await using var entryStream = entry.Open();
        using var output = new MemoryStream();
        await entryStream.CopyToAsync(output, cancellationToken);
        var bytes = output.ToArray();

        if (bytes.Length > MaxSourceFileBytes)
        {
            return new PackageSourceFileResult(StatusCodes.Status413PayloadTooLarge);
        }

        var info = fileTypeMapper.FromPathAndBytes(normalizedRequested, bytes);
        if (info.PreviewKind == PackageSourcePreviewKind.Text || info.IsText)
        {
            var text = Encoding.UTF8.GetString(bytes);
            return new PackageSourceFileResult(
                StatusCodes.Status200OK,
                info.ContentType ?? "text/plain; charset=utf-8",
                text,
                bytes: null,
                previewKind: PackageSourcePreviewKind.Text,
                monacoLanguage: info.MonacoLanguage,
                fileTypeKind: info.Kind);
        }

        return new PackageSourceFileResult(
            StatusCodes.Status200OK,
            info.ContentType ?? "application/octet-stream",
            text: null,
            bytes: bytes,
            previewKind: info.PreviewKind,
            monacoLanguage: info.MonacoLanguage,
            fileTypeKind: info.Kind);
    }

    private static string NormalizeEntryPath(string path)
        => path.Replace('\\', '/').TrimStart('/');

    private static string NameFromPath(string path)
        => path.Split('/').LastOrDefault() ?? path;

    private static string? ParentPath(string path)
    {
        var idx = path.LastIndexOf('/');
        if (idx <= 0)
        {
            return null;
        }

        return path[..idx];
    }

    private static void EnsureDirectories(
        string filePath,
        HashSet<string> seen,
        List<PackageSourceTreeNodeResponse> nodes)
    {
        var parts = filePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length <= 1)
        {
            return;
        }

        var current = string.Empty;
        for (var i = 0; i < parts.Length - 1; i++)
        {
            current = i == 0 ? parts[i] : $"{current}/{parts[i]}";
            if (!seen.Add(current))
            {
                continue;
            }

            nodes.Add(
                new PackageSourceTreeNodeResponse(
                    current,
                    parts[i],
                    true,
                    ParentPath(current),
                    null,
                    "directory",
                    "folder",
                    "none",
                    null,
                    null));
        }
    }
}

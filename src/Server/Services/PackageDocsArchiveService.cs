using System.IO.Compression;
using System.Text;
using Server.Features.Packages;

namespace Server.Services;

public interface IPackageDocsArchiveService
{
    /// <summary>
    /// Lists markdown documentation files from the package artifact (docs/*.md, .beskid/docs/*.md from Beskid pack, and optional README.md).
    /// </summary>
    Task<PackageDocsListResult> ListDocsAsync(
        HttpContext httpContext,
        string idOrName,
        string versionOrLatest,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads raw markdown for a single allowed documentation path.
    /// </summary>
    Task<PackageDocsFileResult> ReadDocAsync(
        HttpContext httpContext,
        string idOrName,
        string versionOrLatest,
        string relativePath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads Beskid-generated structured API documentation (<c>.beskid/docs/api.json</c>) when present.
    /// </summary>
    Task<PackageDocsStructuredResult> ReadStructuredDocAsync(
        HttpContext httpContext,
        string idOrName,
        string versionOrLatest,
        CancellationToken cancellationToken = default);
}

public sealed class PackageDocsListResult(
    int statusCode,
    IReadOnlyList<PackageDocFileEntry>? files = null,
    bool hasStructuredApiDoc = false,
    string? structuredDocRelativePath = null)
{
    public int StatusCode { get; } = statusCode;
    public IReadOnlyList<PackageDocFileEntry>? Files { get; } = files;
    public bool HasStructuredApiDoc { get; } = hasStructuredApiDoc;
    public string? StructuredDocRelativePath { get; } = structuredDocRelativePath;
}

public sealed class PackageDocsFileResult(int statusCode, string? markdown = null, string? contentType = null)
{
    public int StatusCode { get; } = statusCode;
    public string? Markdown { get; } = markdown;
    public string? ContentType { get; } = contentType ?? "text/markdown; charset=utf-8";
}

public sealed class PackageDocsStructuredResult(int statusCode, string? json = null, string? contentType = null)
{
    public int StatusCode { get; } = statusCode;
    public string? Json { get; } = json;
    public string? ContentType { get; } = contentType ?? "application/json; charset=utf-8";
}

public sealed class PackageDocsArchiveService(
    IPackageArtifactExplorerService artifactExplorer) : IPackageDocsArchiveService
{
    public const int MaxListedFiles = 500;
    public const int MaxDocFileBytes = 512 * 1024;

    public async Task<PackageDocsListResult> ListDocsAsync(
        HttpContext httpContext,
        string idOrName,
        string versionOrLatest,
        CancellationToken cancellationToken = default)
    {
        var resolved = await artifactExplorer.ResolveVersionAsync(httpContext, idOrName, versionOrLatest, cancellationToken);
        if (!resolved.IsSuccess)
        {
            return new PackageDocsListResult(resolved.StatusCode);
        }
        var openResult = await artifactExplorer.OpenVerifiedArchiveAsync(resolved.Version!, cancellationToken);
        if (openResult.StatusCode != StatusCodes.Status200OK || openResult.Stream is null)
        {
            return new PackageDocsListResult(openResult.StatusCode);
        }

        using var memory = openResult.Stream;
        using var zip = new ZipArchive(memory, ZipArchiveMode.Read, leaveOpen: false);
        using (zip)
        {
            var entries = new List<PackageDocFileEntry>();
            var hasStructuredApiDoc = false;
            foreach (var entry in zip.Entries)
            {
                if (string.IsNullOrEmpty(entry.Name))
                {
                    continue;
                }

                var full = NormalizeEntryPath(entry.FullName);
                if (PackageDocsPaths.IsStructuredApiDocPath(full))
                {
                    hasStructuredApiDoc = true;
                }

                if (!PackageDocsPaths.IsListableMarkdownPath(full))
                {
                    continue;
                }

                if (entries.Count < MaxListedFiles)
                {
                    entries.Add(new PackageDocFileEntry(full, TitleFromPath(full)));
                }
            }

            var ordered = entries
                .DistinctBy(e => e.Path, StringComparer.OrdinalIgnoreCase)
                .OrderBy(e => e.Path, StringComparer.OrdinalIgnoreCase)
                .ToList();

            // README first when present
            var readme = ordered.FirstOrDefault(e => string.Equals(e.Path, "README.md", StringComparison.OrdinalIgnoreCase));
            if (readme is not null)
            {
                ordered.Remove(readme);
                ordered.Insert(0, readme);
            }

            // Beskid-generated API doc index next (after README) when present
            var apiIndex = ordered.FirstOrDefault(e =>
                string.Equals(e.Path, ".beskid/docs/index.md", StringComparison.OrdinalIgnoreCase));
            if (apiIndex is not null)
            {
                ordered.Remove(apiIndex);
                ordered.Insert(readme is not null ? 1 : 0, apiIndex);
            }

            return new PackageDocsListResult(
                StatusCodes.Status200OK,
                ordered,
                hasStructuredApiDoc,
                hasStructuredApiDoc ? PackageDocsPaths.StructuredApiDocRelativePath : null);
        }
    }

    public async Task<PackageDocsFileResult> ReadDocAsync(
        HttpContext httpContext,
        string idOrName,
        string versionOrLatest,
        string relativePath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return new PackageDocsFileResult(StatusCodes.Status400BadRequest);
        }

        var normalizedRequested = NormalizeEntryPath(relativePath);
        if (!PackageDocsPaths.IsListableMarkdownPath(normalizedRequested))
        {
            return new PackageDocsFileResult(StatusCodes.Status400BadRequest);
        }

        var resolved = await artifactExplorer.ResolveVersionAsync(httpContext, idOrName, versionOrLatest, cancellationToken);
        if (!resolved.IsSuccess)
        {
            return new PackageDocsFileResult(resolved.StatusCode);
        }
        var openResult = await artifactExplorer.OpenVerifiedArchiveAsync(resolved.Version!, cancellationToken);
        if (openResult.StatusCode != StatusCodes.Status200OK || openResult.Stream is null)
        {
            return new PackageDocsFileResult(openResult.StatusCode);
        }

        using var memory = openResult.Stream;
        using var zip = new ZipArchive(memory, ZipArchiveMode.Read, leaveOpen: false);
        using (zip)
        {
            var entry = zip.GetEntry(normalizedRequested)
                        ?? zip.Entries.FirstOrDefault(e =>
                            string.Equals(NormalizeEntryPath(e.FullName), normalizedRequested, StringComparison.OrdinalIgnoreCase));
            if (entry is null || string.IsNullOrEmpty(entry.Name))
            {
                return new PackageDocsFileResult(StatusCodes.Status404NotFound);
            }

            if (entry.Length > MaxDocFileBytes)
            {
                return new PackageDocsFileResult(StatusCodes.Status413PayloadTooLarge);
            }

            await using var entryStream = entry.Open();
            using var reader = new StreamReader(entryStream, Encoding.UTF8);
            var text = await reader.ReadToEndAsync(cancellationToken);
            if (Encoding.UTF8.GetByteCount(text) > MaxDocFileBytes)
            {
                return new PackageDocsFileResult(StatusCodes.Status413PayloadTooLarge);
            }

            return new PackageDocsFileResult(StatusCodes.Status200OK, text);
        }
    }

    public async Task<PackageDocsStructuredResult> ReadStructuredDocAsync(
        HttpContext httpContext,
        string idOrName,
        string versionOrLatest,
        CancellationToken cancellationToken = default)
    {
        var resolved = await artifactExplorer.ResolveVersionAsync(httpContext, idOrName, versionOrLatest, cancellationToken);
        if (!resolved.IsSuccess)
        {
            return new PackageDocsStructuredResult(resolved.StatusCode);
        }
        var openResult = await artifactExplorer.OpenVerifiedArchiveAsync(resolved.Version!, cancellationToken);
        if (openResult.StatusCode != StatusCodes.Status200OK || openResult.Stream is null)
        {
            return new PackageDocsStructuredResult(openResult.StatusCode);
        }

        using var memory = openResult.Stream;
        using var zip = new ZipArchive(memory, ZipArchiveMode.Read, leaveOpen: false);
        using (zip)
        {
            var canonical = PackageDocsPaths.StructuredApiDocRelativePath;
            var entry = zip.GetEntry(canonical)
                        ?? zip.Entries.FirstOrDefault(e =>
                            PackageDocsPaths.IsStructuredApiDocPath(NormalizeEntryPath(e.FullName)));
            if (entry is null || string.IsNullOrEmpty(entry.Name))
            {
                return new PackageDocsStructuredResult(StatusCodes.Status404NotFound);
            }

            if (entry.Length > MaxDocFileBytes)
            {
                return new PackageDocsStructuredResult(StatusCodes.Status413PayloadTooLarge);
            }

            await using var entryStream = entry.Open();
            using var reader = new StreamReader(entryStream, Encoding.UTF8);
            var text = await reader.ReadToEndAsync(cancellationToken);
            if (Encoding.UTF8.GetByteCount(text) > MaxDocFileBytes)
            {
                return new PackageDocsStructuredResult(StatusCodes.Status413PayloadTooLarge);
            }

            return new PackageDocsStructuredResult(StatusCodes.Status200OK, text);
        }
    }
    private static string NormalizeEntryPath(string path)
        => path.Replace('\\', '/').TrimStart('/');

    private static string TitleFromPath(string path)
    {
        var file = path.Split('/').LastOrDefault() ?? path;
        return file.EndsWith(".md", StringComparison.OrdinalIgnoreCase)
            ? file[..^3].Replace('-', ' ')
            : file;
    }
}

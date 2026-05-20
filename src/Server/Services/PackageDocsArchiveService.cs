using System.IO.Compression;
using System.Text;
using Server.Features.Packages;
using Server.Services.Artifacts;

namespace Server.Services;

public interface IPackageDocsArchiveService
{
    Task<PackageDocsListResult> ListDocsAsync(
        HttpContext httpContext,
        string idOrName,
        string versionOrLatest,
        CancellationToken cancellationToken = default);

    Task<PackageDocsFileResult> ReadDocAsync(
        HttpContext httpContext,
        string idOrName,
        string versionOrLatest,
        string relativePath,
        CancellationToken cancellationToken = default);

    Task<PackageDocsStructuredResult> ReadStructuredDocAsync(
        HttpContext httpContext,
        string idOrName,
        string versionOrLatest,
        CancellationToken cancellationToken = default);

    Task<PackageDocsFileResult> ReadReadmeAsync(
        HttpContext httpContext,
        string idOrName,
        string versionOrLatest,
        string? manifestReadmePath,
        CancellationToken cancellationToken = default);
}

public sealed class PackageDocsListResult(int statusCode, IReadOnlyList<PackageDocFileEntry>? files = null, bool hasStructuredApiDoc = false, string? structuredDocRelativePath = null)
    : PackageArtifactResult(statusCode)
{
    public IReadOnlyList<PackageDocFileEntry>? Files { get; } = files;
    public bool HasStructuredApiDoc { get; } = hasStructuredApiDoc;
    public string? StructuredDocRelativePath { get; } = structuredDocRelativePath;
}

public sealed class PackageDocsFileResult(int statusCode, string? markdown = null, string? contentType = null)
    : PackageArtifactResult(statusCode)
{
    public string? Markdown { get; } = markdown;
    public string? ContentType { get; } = contentType ?? "text/markdown; charset=utf-8";
}

public sealed class PackageDocsStructuredResult(int statusCode, string? json = null, string? contentType = null)
    : PackageArtifactResult(statusCode)
{
    public string? Json { get; } = json;
    public string? ContentType { get; } = contentType ?? "application/json; charset=utf-8";
}

public sealed class PackageDocsArchiveService(IPackageArtifactZipReader zipReader) : IPackageDocsArchiveService
{
    public const int MaxListedFiles = 500;
    /// <summary>Markdown and legacy doc files served inline.</summary>
    public const int MaxDocFileBytes = 512 * 1024;
    /// <summary>Structured <c>api.json</c> from Beskid CLI pack (corelib-scale trees exceed markdown cap).</summary>
    public const int MaxStructuredApiDocBytes = 16 * 1024 * 1024;

    public async Task<PackageDocsListResult> ListDocsAsync(
        HttpContext httpContext,
        string idOrName,
        string versionOrLatest,
        CancellationToken cancellationToken = default)
    {
        var (statusCode, listed) = await zipReader.WithZipAsync(
            httpContext,
            idOrName,
            versionOrLatest,
            ListDocsFromZipAsync,
            cancellationToken);

        if (statusCode != StatusCodes.Status200OK || listed is null)
        {
            return new PackageDocsListResult(statusCode);
        }

        return new PackageDocsListResult(
            StatusCodes.Status200OK,
            listed.Files,
            listed.HasStructuredApiDoc,
            listed.StructuredDocRelativePath);
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

        var normalizedRequested = PackageZipPathNormalizer.Normalize(relativePath);
        if (!PackageDocsPaths.IsListableMarkdownPath(normalizedRequested))
        {
            return new PackageDocsFileResult(StatusCodes.Status400BadRequest);
        }

        try
        {
            var (statusCode, markdown) = await zipReader.WithZipAsync(
                httpContext,
                idOrName,
                versionOrLatest,
                (zip, ct) => ReadMarkdownEntryAsync(zip, normalizedRequested, ct),
                cancellationToken);

            if (statusCode != StatusCodes.Status200OK)
            {
                return new PackageDocsFileResult(statusCode);
            }

            if (markdown is null)
            {
                return new PackageDocsFileResult(StatusCodes.Status404NotFound);
            }

            return new PackageDocsFileResult(StatusCodes.Status200OK, markdown);
        }
        catch (PackageArtifactPayloadTooLargeException)
        {
            return new PackageDocsFileResult(StatusCodes.Status413PayloadTooLarge);
        }
    }

    public async Task<PackageDocsFileResult> ReadReadmeAsync(
        HttpContext httpContext,
        string idOrName,
        string versionOrLatest,
        string? manifestReadmePath,
        CancellationToken cancellationToken = default)
    {
        foreach (var path in PackageReadmeResolver.CandidatePaths(manifestReadmePath))
        {
            var result = await ReadDocAsync(httpContext, idOrName, versionOrLatest, path, cancellationToken);
            if (result.StatusCode == StatusCodes.Status200OK && !string.IsNullOrWhiteSpace(result.Markdown))
            {
                return result;
            }

            if (result.StatusCode is not StatusCodes.Status404NotFound)
            {
                return result;
            }
        }

        return new PackageDocsFileResult(StatusCodes.Status404NotFound);
    }

    public async Task<PackageDocsStructuredResult> ReadStructuredDocAsync(
        HttpContext httpContext,
        string idOrName,
        string versionOrLatest,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var (statusCode, json) = await zipReader.WithZipAsync(
                httpContext,
                idOrName,
                versionOrLatest,
                ReadStructuredJsonFromZipAsync,
                cancellationToken);

            if (statusCode != StatusCodes.Status200OK)
            {
                return new PackageDocsStructuredResult(statusCode);
            }

            if (json is null)
            {
                return new PackageDocsStructuredResult(StatusCodes.Status404NotFound);
            }

            return new PackageDocsStructuredResult(StatusCodes.Status200OK, json);
        }
        catch (PackageArtifactPayloadTooLargeException)
        {
            return new PackageDocsStructuredResult(StatusCodes.Status413PayloadTooLarge);
        }
    }

    private static async Task<ListedDocs> ListDocsFromZipAsync(ZipArchive zip, CancellationToken cancellationToken)
    {
        var entries = new List<PackageDocFileEntry>();
        var hasStructuredApiDoc = false;
        foreach (var entry in zip.Entries)
        {
            if (string.IsNullOrEmpty(entry.Name))
            {
                continue;
            }

            var full = PackageZipPathNormalizer.Normalize(entry.FullName);
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

        var readme = ordered.FirstOrDefault(e => string.Equals(e.Path, "README.md", StringComparison.OrdinalIgnoreCase));
        if (readme is not null)
        {
            ordered.Remove(readme);
            ordered.Insert(0, readme);
        }

        var apiIndex = ordered.FirstOrDefault(e =>
            string.Equals(e.Path, ".beskid/docs/index.md", StringComparison.OrdinalIgnoreCase));
        if (apiIndex is not null)
        {
            ordered.Remove(apiIndex);
            ordered.Insert(readme is not null ? 1 : 0, apiIndex);
        }

        return new ListedDocs(
            ordered,
            hasStructuredApiDoc,
            hasStructuredApiDoc ? PackageDocsPaths.StructuredApiDocRelativePath : null);
    }

    private static async Task<string?> ReadMarkdownEntryAsync(
        ZipArchive zip,
        string normalizedRequested,
        CancellationToken cancellationToken)
    {
        var entry = FindEntry(zip, normalizedRequested);
        if (entry is null || string.IsNullOrEmpty(entry.Name))
        {
            return null;
        }

        if (entry.Length > MaxDocFileBytes)
        {
            throw new PackageArtifactPayloadTooLargeException();
        }

        await using var entryStream = entry.Open();
        using var reader = new StreamReader(entryStream, Encoding.UTF8);
        var text = await reader.ReadToEndAsync(cancellationToken);
        if (Encoding.UTF8.GetByteCount(text) > MaxDocFileBytes)
        {
            throw new PackageArtifactPayloadTooLargeException();
        }

        return text;
    }

    private static async Task<string?> ReadStructuredJsonFromZipAsync(ZipArchive zip, CancellationToken cancellationToken)
    {
        var canonical = PackageDocsPaths.StructuredApiDocRelativePath;
        var entry = zip.GetEntry(canonical)
                    ?? zip.Entries.FirstOrDefault(e =>
                        PackageDocsPaths.IsStructuredApiDocPath(PackageZipPathNormalizer.Normalize(e.FullName)));
        if (entry is null || string.IsNullOrEmpty(entry.Name))
        {
            return null;
        }

        if (entry.Length > MaxStructuredApiDocBytes)
        {
            throw new PackageArtifactPayloadTooLargeException();
        }

        await using var entryStream = entry.Open();
        using var reader = new StreamReader(entryStream, Encoding.UTF8);
        var text = await reader.ReadToEndAsync(cancellationToken);
        if (Encoding.UTF8.GetByteCount(text) > MaxStructuredApiDocBytes)
        {
            throw new PackageArtifactPayloadTooLargeException();
        }

        return text;
    }

    private static ZipArchiveEntry? FindEntry(ZipArchive zip, string normalizedRequested)
        => zip.GetEntry(normalizedRequested)
           ?? zip.Entries.FirstOrDefault(e =>
               string.Equals(PackageZipPathNormalizer.Normalize(e.FullName), normalizedRequested, StringComparison.OrdinalIgnoreCase));

    private static string TitleFromPath(string path)
    {
        var file = path.Split('/').LastOrDefault() ?? path;
        return file.EndsWith(".md", StringComparison.OrdinalIgnoreCase)
            ? file[..^3].Replace('-', ' ')
            : file;
    }

    private sealed record ListedDocs(
        IReadOnlyList<PackageDocFileEntry> Files,
        bool HasStructuredApiDoc,
        string? StructuredDocRelativePath);
}

internal sealed class PackageArtifactPayloadTooLargeException : Exception;

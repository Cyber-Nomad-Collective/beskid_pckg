using System.IO.Compression;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Server.Data;
using Server.Features.Packages;

namespace Server.Services;

public interface IPackageDocsArchiveService
{
    /// <summary>
    /// Lists markdown documentation files from the package artifact (docs/*.md and optional README.md).
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
}

public sealed class PackageDocsListResult(
    int statusCode,
    IReadOnlyList<PackageDocFileEntry>? files = null)
{
    public int StatusCode { get; } = statusCode;
    public IReadOnlyList<PackageDocFileEntry>? Files { get; } = files;
}

public sealed class PackageDocsFileResult(int statusCode, string? markdown = null, string? contentType = null)
{
    public int StatusCode { get; } = statusCode;
    public string? Markdown { get; } = markdown;
    public string? ContentType { get; } = contentType ?? "text/markdown; charset=utf-8";
}

public sealed class PackageDocsArchiveService(
    ApplicationDbContext dbContext,
    IPackageArtifactStore artifactStore,
    IApiPrincipalResolver principalResolver) : IPackageDocsArchiveService
{
    public const int MaxListedFiles = 500;
    public const int MaxDocFileBytes = 512 * 1024;

    public async Task<PackageDocsListResult> ListDocsAsync(
        HttpContext httpContext,
        string idOrName,
        string versionOrLatest,
        CancellationToken cancellationToken = default)
    {
        var (version, errStatus) = await ResolveVersionAsync(httpContext, idOrName, versionOrLatest, cancellationToken);
        if (errStatus is int err)
        {
            return new PackageDocsListResult(err);
        }

        if (version is null)
        {
            return new PackageDocsListResult(StatusCodes.Status404NotFound);
        }
        var digestOk = await artifactStore.VerifyChecksumAsync(version.StorageKey, version.ChecksumSha256, cancellationToken);
        if (!digestOk)
        {
            return new PackageDocsListResult(StatusCodes.Status500InternalServerError);
        }

        var opened = await artifactStore.OpenReadAsync(version.StorageKey, cancellationToken);
        if (opened is null)
        {
            return new PackageDocsListResult(StatusCodes.Status404NotFound);
        }

        await using var stream = opened.Value.Stream;
        using var memory = new MemoryStream();
        await stream.CopyToAsync(memory, cancellationToken);
        memory.Position = 0;

        ZipArchive zip;
        try
        {
            zip = new ZipArchive(memory, ZipArchiveMode.Read, leaveOpen: false);
        }
        catch (InvalidDataException)
        {
            return new PackageDocsListResult(StatusCodes.Status500InternalServerError);
        }

        using (zip)
        {
            var entries = new List<PackageDocFileEntry>();
            foreach (var entry in zip.Entries)
            {
                if (string.IsNullOrEmpty(entry.Name))
                {
                    continue;
                }

                var full = NormalizeEntryPath(entry.FullName);
                if (!IsListableDocPath(full))
                {
                    continue;
                }

                entries.Add(new PackageDocFileEntry(full, TitleFromPath(full)));
                if (entries.Count >= MaxListedFiles)
                {
                    break;
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

            return new PackageDocsListResult(StatusCodes.Status200OK, ordered);
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
        if (!IsReadableDocPath(normalizedRequested))
        {
            return new PackageDocsFileResult(StatusCodes.Status400BadRequest);
        }

        var (version, errStatus) = await ResolveVersionAsync(httpContext, idOrName, versionOrLatest, cancellationToken);
        if (errStatus is int err)
        {
            return new PackageDocsFileResult(err);
        }

        if (version is null)
        {
            return new PackageDocsFileResult(StatusCodes.Status404NotFound);
        }
        var digestOk = await artifactStore.VerifyChecksumAsync(version.StorageKey, version.ChecksumSha256, cancellationToken);
        if (!digestOk)
        {
            return new PackageDocsFileResult(StatusCodes.Status500InternalServerError);
        }

        var opened = await artifactStore.OpenReadAsync(version.StorageKey, cancellationToken);
        if (opened is null)
        {
            return new PackageDocsFileResult(StatusCodes.Status404NotFound);
        }

        await using var stream = opened.Value.Stream;
        using var memory = new MemoryStream();
        await stream.CopyToAsync(memory, cancellationToken);
        memory.Position = 0;

        ZipArchive zip;
        try
        {
            zip = new ZipArchive(memory, ZipArchiveMode.Read, leaveOpen: false);
        }
        catch (InvalidDataException)
        {
            return new PackageDocsFileResult(StatusCodes.Status500InternalServerError);
        }

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

    private async Task<(PackageVersionEntity? Version, int? ErrorStatus)> ResolveVersionAsync(
        HttpContext httpContext,
        string idOrName,
        string versionOrLatest,
        CancellationToken cancellationToken)
    {
        var key = idOrName?.Trim();
        if (string.IsNullOrWhiteSpace(key))
        {
            return (null, StatusCodes.Status400BadRequest);
        }

        var verRaw = versionOrLatest?.Trim();
        if (string.IsNullOrWhiteSpace(verRaw))
        {
            return (null, StatusCodes.Status400BadRequest);
        }

        var package = Guid.TryParse(key, out var packageId)
            ? await dbContext.Packages.AsNoTracking().SingleOrDefaultAsync(x => x.Id == packageId, cancellationToken)
            : await dbContext.Packages.AsNoTracking().SingleOrDefaultAsync(x => x.Name == key, cancellationToken);

        if (package is null)
        {
            return (null, StatusCodes.Status404NotFound);
        }

        if (!package.IsPublic)
        {
            var userId = await principalResolver.ResolveUserIdAsync(httpContext, cancellationToken);
            if (string.IsNullOrWhiteSpace(userId) || userId != package.OwnerUserId)
            {
                return (null, StatusCodes.Status404NotFound);
            }
        }

        PackageVersionEntity? versionEntity;
        if (string.Equals(verRaw, "latest", StringComparison.OrdinalIgnoreCase))
        {
            versionEntity = await dbContext.PackageVersions
                .AsNoTracking()
                .Where(x => x.PackageId == package.Id && !x.IsYanked)
                .OrderByDescending(x => x.PublishedAtUtc)
                .FirstOrDefaultAsync(cancellationToken);
        }
        else
        {
            versionEntity = await dbContext.PackageVersions
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    x => x.PackageId == package.Id && x.Version == verRaw && !x.IsYanked,
                    cancellationToken);
        }

        if (versionEntity is null)
        {
            return (null, StatusCodes.Status404NotFound);
        }

        return (versionEntity, null);
    }

    private static string NormalizeEntryPath(string path)
        => path.Replace('\\', '/').TrimStart('/');

    private static bool HasOnlySafePathSegments(string normalized)
    {
        foreach (var segment in normalized.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (segment is "." or "..")
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsListableDocPath(string normalized)
    {
        if (!HasOnlySafePathSegments(normalized))
        {
            return false;
        }

        if (string.Equals(normalized, "README.md", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return normalized.StartsWith("docs/", StringComparison.OrdinalIgnoreCase)
               && normalized.EndsWith(".md", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsReadableDocPath(string normalized) => IsListableDocPath(normalized);

    private static string TitleFromPath(string path)
    {
        var file = path.Split('/').LastOrDefault() ?? path;
        return file.EndsWith(".md", StringComparison.OrdinalIgnoreCase)
            ? file[..^3].Replace('-', ' ')
            : file;
    }
}

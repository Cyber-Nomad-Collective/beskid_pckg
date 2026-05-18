using System.IO.Compression;
using Microsoft.EntityFrameworkCore;
using Server.Data;

namespace Server.Services;

public interface IPackageArtifactExplorerService
{
    Task<PackageArtifactVersionResolutionResult> ResolveVersionAsync(
        HttpContext httpContext,
        string idOrName,
        string versionOrLatest,
        CancellationToken cancellationToken = default);

    Task<PackageArtifactArchiveReadResult> OpenVerifiedArchiveAsync(
        PackageVersionEntity version,
        CancellationToken cancellationToken = default);
}

public sealed record PackageArtifactVersionResolutionResult(
    int StatusCode,
    PackageEntity? Package = null,
    PackageVersionEntity? Version = null)
{
    public bool IsSuccess =>
        StatusCode == StatusCodes.Status200OK && Package is not null && Version is not null;
}

public sealed class PackageArtifactArchiveReadResult(
    int statusCode,
    MemoryStream? stream = null)
{
    public int StatusCode { get; } = statusCode;
    public MemoryStream? Stream { get; } = stream;
}

public sealed class PackageArtifactExplorerService(
    ApplicationDbContext dbContext,
    IPackageArtifactStore artifactStore,
    IPackageAccessService packageAccess) : IPackageArtifactExplorerService
{
    public async Task<PackageArtifactVersionResolutionResult> ResolveVersionAsync(
        HttpContext httpContext,
        string idOrName,
        string versionOrLatest,
        CancellationToken cancellationToken = default)
    {
        var key = idOrName?.Trim();
        if (string.IsNullOrWhiteSpace(key))
        {
            return new PackageArtifactVersionResolutionResult(StatusCodes.Status400BadRequest);
        }

        var verRaw = versionOrLatest?.Trim();
        if (string.IsNullOrWhiteSpace(verRaw))
        {
            return new PackageArtifactVersionResolutionResult(StatusCodes.Status400BadRequest);
        }

        var package = Guid.TryParse(key, out var packageId)
            ? await dbContext.Packages.AsNoTracking().SingleOrDefaultAsync(x => x.Id == packageId, cancellationToken)
            : await dbContext.Packages.AsNoTracking().SingleOrDefaultAsync(x => x.Name == key, cancellationToken);

        if (package is null)
        {
            return new PackageArtifactVersionResolutionResult(StatusCodes.Status404NotFound);
        }

        if (!await packageAccess.CanViewPackageAsync(httpContext, package, cancellationToken))
        {
            return new PackageArtifactVersionResolutionResult(StatusCodes.Status404NotFound);
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
            return new PackageArtifactVersionResolutionResult(StatusCodes.Status404NotFound);
        }

        return new PackageArtifactVersionResolutionResult(
            StatusCodes.Status200OK,
            package,
            versionEntity);
    }

    public async Task<PackageArtifactArchiveReadResult> OpenVerifiedArchiveAsync(
        PackageVersionEntity version,
        CancellationToken cancellationToken = default)
    {
        var digestOk = await artifactStore.VerifyChecksumAsync(version.StorageKey, version.ChecksumSha256, cancellationToken);
        if (!digestOk)
        {
            return new PackageArtifactArchiveReadResult(StatusCodes.Status500InternalServerError);
        }

        var opened = await artifactStore.OpenReadAsync(version.StorageKey, cancellationToken);
        if (opened is null)
        {
            return new PackageArtifactArchiveReadResult(StatusCodes.Status404NotFound);
        }

        await using var source = opened.Value.Stream;
        var memory = new MemoryStream();
        await source.CopyToAsync(memory, cancellationToken);
        memory.Position = 0;

        try
        {
            // Validate ZIP payload here so callers can trust stream.
            using var _ = new ZipArchive(memory, ZipArchiveMode.Read, leaveOpen: true);
            memory.Position = 0;
            return new PackageArtifactArchiveReadResult(StatusCodes.Status200OK, memory);
        }
        catch (InvalidDataException)
        {
            memory.Dispose();
            return new PackageArtifactArchiveReadResult(StatusCodes.Status500InternalServerError);
        }
    }
}

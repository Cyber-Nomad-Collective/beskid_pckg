using System.IO.Compression;

namespace Server.Services.Artifacts;

public interface IPackageArtifactZipReader
{
    Task<(int StatusCode, T? Result)> WithZipAsync<T>(
        HttpContext httpContext,
        string idOrName,
        string versionOrLatest,
        Func<ZipArchive, CancellationToken, Task<T>> action,
        CancellationToken cancellationToken = default);
}

public sealed class PackageArtifactZipReader(IPackageArtifactExplorerService explorer) : IPackageArtifactZipReader
{
    public async Task<(int StatusCode, T? Result)> WithZipAsync<T>(
        HttpContext httpContext,
        string idOrName,
        string versionOrLatest,
        Func<ZipArchive, CancellationToken, Task<T>> action,
        CancellationToken cancellationToken = default)
    {
        var resolved = await explorer.ResolveVersionAsync(httpContext, idOrName, versionOrLatest, cancellationToken);
        if (!resolved.IsSuccess || resolved.Version is null)
        {
            return (resolved.StatusCode, default);
        }

        var opened = await explorer.OpenVerifiedArchiveAsync(resolved.Version, cancellationToken);
        if (opened.StatusCode != StatusCodes.Status200OK || opened.Stream is null)
        {
            opened.Stream?.Dispose();
            return (opened.StatusCode, default);
        }

        await using var stream = opened.Stream;
        using var zip = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
        var result = await action(zip, cancellationToken);
        return (StatusCodes.Status200OK, result);
    }
}

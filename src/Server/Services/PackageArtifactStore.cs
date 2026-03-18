using System.Security.Cryptography;

namespace Server.Services;

public interface IPackageArtifactStore
{
    Task<(string StorageKey, string ChecksumSha256, long SizeBytes)> SaveAsync(
        string packageName,
        string version,
        Stream artifact,
        CancellationToken cancellationToken = default);

    Task<(Stream Stream, string ContentType, long? SizeBytes)?> OpenReadAsync(
        string storageKey,
        CancellationToken cancellationToken = default);

    Task<bool> VerifyChecksumAsync(
        string storageKey,
        string expectedSha256,
        CancellationToken cancellationToken = default);
}

public sealed class PackageArtifactStore(IHostEnvironment hostEnvironment, IConfiguration configuration) : IPackageArtifactStore
{
    private readonly string artifactsRoot = ResolveArtifactsRoot(hostEnvironment, configuration);

    public async Task<(string StorageKey, string ChecksumSha256, long SizeBytes)> SaveAsync(
        string packageName,
        string version,
        Stream artifact,
        CancellationToken cancellationToken = default)
    {
        var safePackage = Sanitize(packageName);
        var safeVersion = Sanitize(version);
        var relativePath = Path.Combine(safePackage, safeVersion, "artifact.bpk");
        var absolutePath = Path.Combine(artifactsRoot, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(absolutePath)!);

        await using var target = File.Create(absolutePath);
        using var hasher = SHA256.Create();

        var buffer = new byte[81920];
        long size = 0;
        while (true)
        {
            var read = await artifact.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
            if (read <= 0)
            {
                break;
            }

            await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            hasher.TransformBlock(buffer, 0, read, null, 0);
            size += read;
        }

        hasher.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
        var digest = Convert.ToHexString(hasher.Hash!).ToLowerInvariant();

        return (relativePath.Replace('\\', '/'), digest, size);
    }

    public Task<(Stream Stream, string ContentType, long? SizeBytes)?> OpenReadAsync(
        string storageKey,
        CancellationToken cancellationToken = default)
    {
        var absolutePath = ResolveStoragePath(storageKey);
        if (absolutePath is null)
        {
            return Task.FromResult<(Stream Stream, string ContentType, long? SizeBytes)?>(null);
        }

        if (!File.Exists(absolutePath))
        {
            return Task.FromResult<(Stream Stream, string ContentType, long? SizeBytes)?>(null);
        }

        var stream = (Stream)File.OpenRead(absolutePath);
        long? size = new FileInfo(absolutePath).Length;
        return Task.FromResult<(Stream Stream, string ContentType, long? SizeBytes)?>(
            (stream, "application/zip", size));
    }

    public async Task<bool> VerifyChecksumAsync(
        string storageKey,
        string expectedSha256,
        CancellationToken cancellationToken = default)
    {
        var absolutePath = ResolveStoragePath(storageKey);
        if (absolutePath is null)
        {
            return false;
        }

        if (!File.Exists(absolutePath))
        {
            return false;
        }

        await using var stream = File.OpenRead(absolutePath);
        using var sha = SHA256.Create();
        var digest = Convert.ToHexString(await sha.ComputeHashAsync(stream, cancellationToken)).ToLowerInvariant();
        return string.Equals(digest, expectedSha256, StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolveArtifactsRoot(IHostEnvironment hostEnvironment, IConfiguration configuration)
    {
        var configuredPath = configuration["Storage:ArtifactsRootPath"];
        var root = string.IsNullOrWhiteSpace(configuredPath)
            ? Path.Combine(hostEnvironment.ContentRootPath, "App_Data", "artifacts")
            : configuredPath;

        if (!Path.IsPathRooted(root))
        {
            root = Path.Combine(hostEnvironment.ContentRootPath, root);
        }

        root = Path.GetFullPath(root);
        Directory.CreateDirectory(root);
        return root;
    }

    private string? ResolveStoragePath(string storageKey)
    {
        var relativePath = storageKey.Replace('/', Path.DirectorySeparatorChar);
        var fullPath = Path.GetFullPath(Path.Combine(artifactsRoot, relativePath));
        if (!fullPath.StartsWith(artifactsRoot, StringComparison.Ordinal))
        {
            return null;
        }

        return fullPath;
    }

    private static string Sanitize(string value)
    {
        var chars = value
            .Trim()
            .ToLowerInvariant()
            .Select(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_' or '.' ? ch : '_')
            .ToArray();
        return new string(chars);
    }
}

using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Server.Services;

public interface IPackageArtifactValidator
{
    /// <param name="relaxPackageJsonVersion">When true, <c>package.json</c> <c>version</c> may differ from <paramref name="expectedVersion"/> (registry-assigned publish).</param>
    Task<PackageArtifactValidationResult> ValidateAsync(
        Stream artifact,
        string expectedPackageName,
        string expectedVersion,
        bool relaxPackageJsonVersion = false,
        CancellationToken cancellationToken = default);
}

public sealed record PackageArtifactValidationResult(
    bool IsValid,
    string Message,
    string? ArtifactChecksumSha256 = null,
    string? ManifestJson = null);

public sealed class PackageArtifactValidator(IOptions<PackagePublishOptions> publishOptions) : IPackageArtifactValidator
{
    private readonly PackagePublishOptions _publishOptions = publishOptions.Value;

    private static readonly HashSet<string> RequiredEntries =
    [
        "package.json",
        "Project.proj",
        "checksums.sha256",
    ];

    public async Task<PackageArtifactValidationResult> ValidateAsync(
        Stream artifact,
        string expectedPackageName,
        string expectedVersion,
        bool relaxPackageJsonVersion = false,
        CancellationToken cancellationToken = default)
    {
        using var memory = new MemoryStream();
        await artifact.CopyToAsync(memory, cancellationToken);
        var archiveBytes = memory.ToArray();
        if (archiveBytes.Length == 0)
        {
            return new(false, "Artifact is empty.");
        }

        string archiveDigest;
        using (var sha = SHA256.Create())
        {
            archiveDigest = Convert.ToHexString(sha.ComputeHash(archiveBytes)).ToLowerInvariant();
        }

        memory.Position = 0;

        ZipArchive zip;
        try
        {
            zip = new ZipArchive(memory, ZipArchiveMode.Read, leaveOpen: true);
        }
        catch (InvalidDataException)
        {
            return new(false, "Artifact is not a valid ZIP archive.");
        }

        using (zip)
        {
            var fileEntries = zip.Entries
                .Where(e => !string.IsNullOrEmpty(e.Name))
                .ToDictionary(e => NormalizeEntryPath(e.FullName), StringComparer.Ordinal);

            foreach (var required in RequiredEntries)
            {
                if (!fileEntries.ContainsKey(required))
                {
                    return new(false, $"Missing required artifact entry '{required}'.");
                }
            }

            var forbiddenEntry = fileEntries.Keys.FirstOrDefault(IsForbiddenEntryPath);
            if (forbiddenEntry is not null)
            {
                return new(false, $"Artifact contains forbidden entry '{forbiddenEntry}'.");
            }

            if (!fileEntries.Keys.Any(path => path.StartsWith("src/", StringComparison.Ordinal)))
            {
                return new(false, "Artifact must include at least one file under src/.");
            }

            if (_publishOptions.RequireStructuredApiDoc
                && !fileEntries.ContainsKey(PackageDocsPaths.StructuredApiDocRelativePath))
            {
                return new(
                    false,
                    $"Artifact must include '{PackageDocsPaths.StructuredApiDocRelativePath}'. "
                    + "Run `beskid pckg pack` or `beskid doc --project Project.proj --out .beskid/docs` before publishing.");
            }

            var packageJsonText = await ReadEntryTextAsync(fileEntries["package.json"], cancellationToken);
            var projectManifestText = await ReadEntryTextAsync(fileEntries["Project.proj"], cancellationToken);
            var checksumsText = await ReadEntryTextAsync(fileEntries["checksums.sha256"], cancellationToken);

            var packageJsonValidation = ValidatePackageJson(packageJsonText, expectedPackageName, expectedVersion, relaxPackageJsonVersion);
            if (!packageJsonValidation.IsValid)
            {
                return packageJsonValidation;
            }

            if (!ProjectManifestContainsPackageName(projectManifestText, expectedPackageName))
            {
                return new(false, "Project.proj does not match package name from package.json.");
            }

            var parseChecksumsResult = ParseChecksums(checksumsText);
            if (!parseChecksumsResult.IsValid)
            {
                return new(false, parseChecksumsResult.Message);
            }

            var checksums = parseChecksumsResult.Checksums!;

            if (checksums.ContainsKey("checksums.sha256"))
            {
                return new(false, "checksums.sha256 must not contain a checksum entry for itself.");
            }

            foreach (var entryPath in fileEntries.Keys.Where(path => path != "checksums.sha256"))
            {
                if (!checksums.ContainsKey(entryPath))
                {
                    return new(false, $"checksums.sha256 is missing entry for '{entryPath}'.");
                }
            }

            foreach (var checksumEntry in checksums)
            {
                if (!fileEntries.TryGetValue(checksumEntry.Key, out var zipEntry))
                {
                    return new(false, $"checksums.sha256 references missing file '{checksumEntry.Key}'.");
                }

                var entryDigest = await ComputeEntrySha256Async(zipEntry, cancellationToken);
                if (!string.Equals(entryDigest, checksumEntry.Value, StringComparison.OrdinalIgnoreCase))
                {
                    return new(false, $"Checksum mismatch for '{checksumEntry.Key}'.");
                }
            }

            if (fileEntries.TryGetValue(PackageDocsPaths.StructuredApiDocRelativePath, out var apiJsonEntry))
            {
                var apiJsonText = await ReadEntryTextAsync(apiJsonEntry, cancellationToken);
                var apiValidation = StructuredApiDocValidator.ValidateJson(apiJsonText);
                if (!apiValidation.IsValid)
                {
                    return new(false, apiValidation.Message);
                }
            }

            return new(true, "Artifact validated.", archiveDigest, packageJsonText);
        }
    }

    private static PackageArtifactValidationResult ValidatePackageJson(
        string packageJsonText,
        string expectedPackageName,
        string expectedVersion,
        bool relaxPackageJsonVersion)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(packageJsonText);
        }
        catch (JsonException)
        {
            return new(false, "package.json is not valid JSON.");
        }

        using (document)
        {
            var root = document.RootElement;
            var schema = root.TryGetProperty("schema", out var schemaProp) ? schemaProp.GetString() : null;
            var id = root.TryGetProperty("id", out var idProp) ? idProp.GetString() : null;
            var version = root.TryGetProperty("version", out var versionProp) ? versionProp.GetString() : null;

            if (!string.Equals(schema, "beskid.package.v1", StringComparison.Ordinal))
            {
                return new(false, "package.json schema must be 'beskid.package.v1'.");
            }

            if (!string.Equals(id, expectedPackageName, StringComparison.OrdinalIgnoreCase))
            {
                return new(false, "package.json id does not match package name in route.");
            }

            if (!relaxPackageJsonVersion
                && !string.Equals(version, expectedVersion, StringComparison.Ordinal))
            {
                return new(false, "package.json version does not match publish version.");
            }

            var dependencyValidation = ValidateConsumerDependencies(packageJsonText);
            if (!dependencyValidation.IsValid)
            {
                return dependencyValidation;
            }

            return new(true, "package.json validated.");
        }
    }

    private static PackageArtifactValidationResult ValidateConsumerDependencies(string packageJsonText)
    {
        var metadata = PackageManifestMetadataReader.Read(packageJsonText);
        foreach (var dependency in metadata.Dependencies)
        {
            if (string.Equals(dependency.Source, "registry", StringComparison.OrdinalIgnoreCase)
                || string.Equals(dependency.Source, "pckg", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(dependency.Version))
                {
                    return new(false, $"package.json dependency '{dependency.Name}' must include a version for registry consumers.");
                }

                continue;
            }

            if (string.Equals(dependency.Source, "path", StringComparison.OrdinalIgnoreCase)
                || string.Equals(dependency.Source, "workspace", StringComparison.OrdinalIgnoreCase))
            {
                return new(false, $"package.json dependency '{dependency.Name}' must not use source '{dependency.Source}' in published artifacts.");
            }

            return new(false, $"package.json dependency '{dependency.Name}' must use registry source for published artifacts.");
        }

        return new(true, "package.json dependencies validated.");
    }

    private static bool ProjectManifestContainsPackageName(string projectManifest, string packageName)
    {
        var normalized = projectManifest.Replace("\r", string.Empty);
        var nameNeedle = $"name = \"{packageName}\"";
        return normalized.Contains(nameNeedle, StringComparison.OrdinalIgnoreCase);
    }

    private static (bool IsValid, string Message, Dictionary<string, string>? Checksums) ParseChecksums(string checksumsText)
    {
        var checksums = new Dictionary<string, string>(StringComparer.Ordinal);
        var lines = checksumsText.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var line in lines)
        {
            if (line.StartsWith('#'))
            {
                continue;
            }

            var split = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (split.Length < 2)
            {
                return (false, $"Invalid checksums entry: '{line}'.", null);
            }

            var digest = split[0].Trim().ToLowerInvariant();
            var path = NormalizeEntryPath(split[^1].Trim());

            if (string.IsNullOrWhiteSpace(path))
            {
                return (false, $"Invalid checksums path in line '{line}'.", null);
            }

            checksums[path] = digest;
        }

        return (true, "checksums parsed.", checksums);
    }

    private static async Task<string> ComputeEntrySha256Async(ZipArchiveEntry entry, CancellationToken cancellationToken)
    {
        await using var stream = entry.Open();
        using var sha = SHA256.Create();
        var hash = await sha.ComputeHashAsync(stream, cancellationToken);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static async Task<string> ReadEntryTextAsync(ZipArchiveEntry entry, CancellationToken cancellationToken)
    {
        await using var stream = entry.Open();
        using var reader = new StreamReader(stream);
        cancellationToken.ThrowIfCancellationRequested();
        return await reader.ReadToEndAsync(cancellationToken);
    }

    private static string NormalizeEntryPath(string path)
    {
        return path.Replace('\\', '/').TrimStart('/');
    }

    private static bool IsForbiddenEntryPath(string path)
    {
        // Allow Beskid CLI generated package docs under .beskid/docs/ (markdown + api.json).
        if (path.StartsWith(".beskid/docs/", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return path.StartsWith(".beskid/", StringComparison.OrdinalIgnoreCase)
               || string.Equals(path, ".beskid", StringComparison.OrdinalIgnoreCase);
    }
}

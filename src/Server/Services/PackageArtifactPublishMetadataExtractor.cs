using System.IO.Compression;
using System.Text;
using Server.Services.Artifacts;

namespace Server.Services;

public sealed record PackageArtifactPublishMetadata(
    string? ReadmeMarkdown,
    string? ConfigurationJson,
    string? OverridesJson,
    string? IconUrl);

public interface IPackageArtifactPublishMetadataExtractor
{
    PackageArtifactPublishMetadata Extract(Stream artifactZip, string packageJsonText);
}

/// <summary>
/// Reads publish-time metadata from a validated <c>.bpk</c> ZIP (readme body, manifest configuration/overrides, icon URL).
/// </summary>
public sealed class PackageArtifactPublishMetadataExtractor : IPackageArtifactPublishMetadataExtractor
{
    public PackageArtifactPublishMetadata Extract(Stream artifactZip, string packageJsonText)
    {
        var manifest = PackageManifestMetadataReader.Read(packageJsonText);
        if (artifactZip.CanSeek)
        {
            artifactZip.Position = 0;
        }

        using var memory = new MemoryStream();
        artifactZip.CopyTo(memory);
        memory.Position = 0;

        ZipArchive zip;
        try
        {
            zip = new ZipArchive(memory, ZipArchiveMode.Read, leaveOpen: true);
        }
        catch (InvalidDataException)
        {
            return new PackageArtifactPublishMetadata(null, manifest.ConfigurationJson, manifest.OverridesJson, manifest.IconUrl);
        }

        using (zip)
        {
            var readmeMarkdown = TryReadReadmeMarkdown(zip, manifest.ReadmePath);
            return new PackageArtifactPublishMetadata(
                readmeMarkdown,
                manifest.ConfigurationJson,
                manifest.OverridesJson,
                manifest.IconUrl);
        }
    }

    private static string? TryReadReadmeMarkdown(ZipArchive zip, string? manifestReadmePath)
    {
        foreach (var path in PackageReadmeResolver.CandidatePaths(manifestReadmePath))
        {
            var text = TryReadTextEntry(zip, path);
            if (!string.IsNullOrWhiteSpace(text))
            {
                return text;
            }
        }

        return null;
    }

    private static string? TryReadTextEntry(ZipArchive zip, string normalizedPath)
    {
        var entry = zip.GetEntry(normalizedPath)
                    ?? zip.Entries.FirstOrDefault(e =>
                        string.Equals(
                            PackageZipPathNormalizer.Normalize(e.FullName),
                            normalizedPath,
                            StringComparison.OrdinalIgnoreCase));

        if (entry is null || string.IsNullOrEmpty(entry.Name))
        {
            return null;
        }

        if (entry.Length > PackageDocsArchiveService.MaxDocFileBytes)
        {
            return null;
        }

        using var stream = entry.Open();
        using var reader = new StreamReader(stream, Encoding.UTF8);
        var text = reader.ReadToEnd();
        return Encoding.UTF8.GetByteCount(text) > PackageDocsArchiveService.MaxDocFileBytes ? null : text;
    }
}

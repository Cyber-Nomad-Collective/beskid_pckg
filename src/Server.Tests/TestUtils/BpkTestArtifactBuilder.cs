using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Server.Tests.TestUtils;

internal static class BpkTestArtifactBuilder
{
    public static byte[] CreateValidArtifact(string packageName, string version)
    {
        var packageJson = JsonSerializer.Serialize(new
        {
            schema = "beskid.package.v1",
            id = packageName,
            version,
        });

        var projectProj = $"project {{\n  name = \"{packageName}\"\n  version = \"{version}\"\n}}\n\n";
        var srcMain = "fn Main() {}\n";

        var entries = new Dictionary<string, byte[]>
        {
            ["package.json"] = Encoding.UTF8.GetBytes(packageJson),
            ["Project.proj"] = Encoding.UTF8.GetBytes(projectProj),
            ["src/Main.bd"] = Encoding.UTF8.GetBytes(srcMain),
        };

        var checksumLines = entries
            .OrderBy(kv => kv.Key, StringComparer.Ordinal)
            .Select(kv => $"{Sha256(kv.Value)}  {kv.Key}");

        entries["checksums.sha256"] = Encoding.UTF8.GetBytes(string.Join('\n', checksumLines) + "\n");

        return CreateZip(entries);
    }

    public static byte[] CreateArtifactWithBadChecksum(string packageName, string version)
    {
        var packageJson = JsonSerializer.Serialize(new
        {
            schema = "beskid.package.v1",
            id = packageName,
            version,
        });

        var projectProj = $"project {{\n  name = \"{packageName}\"\n  version = \"{version}\"\n}}\n\n";
        var srcMain = "fn Main() {}\n";

        var entries = new Dictionary<string, byte[]>
        {
            ["package.json"] = Encoding.UTF8.GetBytes(packageJson),
            ["Project.proj"] = Encoding.UTF8.GetBytes(projectProj),
            ["src/Main.bd"] = Encoding.UTF8.GetBytes(srcMain),
            ["checksums.sha256"] = Encoding.UTF8.GetBytes("deadbeef  package.json\n"),
        };

        return CreateZip(entries);
    }

    public static string ArtifactSha256(byte[] artifactBytes) => Sha256(artifactBytes);

    private static byte[] CreateZip(IReadOnlyDictionary<string, byte[]> entries)
    {
        using var output = new MemoryStream();
        using (var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var kv in entries.OrderBy(k => k.Key, StringComparer.Ordinal))
            {
                var entry = zip.CreateEntry(kv.Key, CompressionLevel.NoCompression);
                using var stream = entry.Open();
                stream.Write(kv.Value);
            }
        }

        return output.ToArray();
    }

    private static string Sha256(byte[] bytes)
    {
        using var sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(bytes)).ToLowerInvariant();
    }
}

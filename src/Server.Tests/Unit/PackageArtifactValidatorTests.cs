using Server.Services;
using Server.Tests.TestUtils;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Server.Tests.Unit;

public class PackageArtifactValidatorTests
{
    private readonly PackageArtifactValidator _validator = new();

    [Fact]
    public async Task ValidateAsync_Accepts_Compliant_Artifact()
    {
        var bytes = BpkTestArtifactBuilder.CreateValidArtifact("Demo", "1.2.3");
        await using var stream = new MemoryStream(bytes);

        var result = await _validator.ValidateAsync(stream, "Demo", "1.2.3");

        Assert.True(result.IsValid);
        Assert.Equal("Artifact validated.", result.Message);
        Assert.Equal(BpkTestArtifactBuilder.ArtifactSha256(bytes), result.ArtifactChecksumSha256);
        Assert.Contains("\"schema\":\"beskid.package.v1\"", result.ManifestJson);
    }

    [Fact]
    public async Task ValidateAsync_Rejects_Checksum_Mismatch()
    {
        var bytes = BpkTestArtifactBuilder.CreateArtifactWithBadChecksum("Demo", "1.2.3");
        await using var stream = new MemoryStream(bytes);

        var result = await _validator.ValidateAsync(stream, "Demo", "1.2.3");

        Assert.False(result.IsValid);
        Assert.True(
            result.Message.Contains("checksums.sha256", StringComparison.OrdinalIgnoreCase)
            || result.Message.Contains("Checksum mismatch", StringComparison.OrdinalIgnoreCase),
            result.Message);
    }

    [Fact]
    public async Task ValidateAsync_Rejects_Version_Mismatch()
    {
        var bytes = BpkTestArtifactBuilder.CreateValidArtifact("Demo", "2.0.0");
        await using var stream = new MemoryStream(bytes);

        var result = await _validator.ValidateAsync(stream, "Demo", "1.0.0");

        Assert.False(result.IsValid);
        Assert.Contains("package.json version", result.Message);
    }

    [Fact]
    public async Task ValidateAsync_Rejects_Artifacts_With_Beskid_Config_Entries()
    {
        var packageJson = JsonSerializer.Serialize(new
        {
            schema = "beskid.package.v1",
            id = "Demo",
            version = "1.2.3",
        });

        var entries = new Dictionary<string, byte[]>
        {
            ["package.json"] = Encoding.UTF8.GetBytes(packageJson),
            ["Project.proj"] = Encoding.UTF8.GetBytes("project {\n  name = \"Demo\"\n}\n"),
            ["src/Main.bd"] = Encoding.UTF8.GetBytes("fn Main() {}\n"),
            [".beskid/pckg/repositories.json"] = Encoding.UTF8.GetBytes("{\"repositories\":{}}"),
        };

        var checksumLines = entries
            .OrderBy(kv => kv.Key, StringComparer.Ordinal)
            .Select(kv => $"{Sha256(kv.Value)}  {kv.Key}");
        entries["checksums.sha256"] = Encoding.UTF8.GetBytes(string.Join('\n', checksumLines) + "\n");

        await using var stream = new MemoryStream(CreateZip(entries));
        var result = await _validator.ValidateAsync(stream, "Demo", "1.2.3");

        Assert.False(result.IsValid);
        Assert.Contains("forbidden entry", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(".beskid", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static string Sha256(byte[] bytes)
    {
        using var sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(bytes)).ToLowerInvariant();
    }

    private static byte[] CreateZip(IReadOnlyDictionary<string, byte[]> entries)
    {
        using var output = new MemoryStream();
        using (var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var kv in entries.OrderBy(k => k.Key, StringComparer.Ordinal))
            {
                var entry = zip.CreateEntry(kv.Key, CompressionLevel.NoCompression);
                using var entryStream = entry.Open();
                entryStream.Write(kv.Value);
            }
        }

        return output.ToArray();
    }
}

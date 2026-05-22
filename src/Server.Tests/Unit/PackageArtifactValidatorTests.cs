using Server.Services;
using Server.Tests.TestUtils;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Server.Tests.Unit;

public class PackageArtifactValidatorTests
{
    private readonly PackageArtifactValidator _validator = new(
        Microsoft.Extensions.Options.Options.Create(new PackagePublishOptions
        {
            RequireStructuredApiDoc = true,
        }));

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
    public async Task ValidateAsync_Accepts_BeskidDocs_ApiJson_Under_Docs_Tree()
    {
        var extra = new Dictionary<string, string>
        {
            [".beskid/docs/api.json"] = BpkTestArtifactBuilder.MinimalStructuredApiJson,
        };
        var bytes = BpkTestArtifactBuilder.CreateValidArtifact("Demo", "1.2.3", extra);
        await using var stream = new MemoryStream(bytes);

        var result = await _validator.ValidateAsync(stream, "Demo", "1.2.3");

        Assert.True(result.IsValid, result.Message);
    }

    [Fact]
    public async Task ValidateAsync_Rejects_Path_Dependencies_In_PackageJson()
    {
        var packageJson = JsonSerializer.Serialize(new
        {
            schema = "beskid.package.v1",
            id = "Demo",
            version = "1.0.0",
            dependencies = new Dictionary<string, object>
            {
                ["Other"] = new { source = "path", path = "../Other" },
            },
        });

        var entries = new Dictionary<string, byte[]>
        {
            ["package.json"] = Encoding.UTF8.GetBytes(packageJson),
            ["Project.proj"] = Encoding.UTF8.GetBytes("name = \"Demo\"\n"),
            ["src/Main.bd"] = Encoding.UTF8.GetBytes("// demo"),
            [".beskid/docs/api.json"] = Encoding.UTF8.GetBytes(BpkTestArtifactBuilder.MinimalStructuredApiJson),
        };
        var checksumLines = entries
            .OrderBy(kv => kv.Key, StringComparer.Ordinal)
            .Select(kv => $"{Sha256(kv.Value)}  {kv.Key}");
        entries["checksums.sha256"] = Encoding.UTF8.GetBytes(string.Join('\n', checksumLines) + "\n");

        await using var stream = new MemoryStream(CreateZip(entries));
        var result = await _validator.ValidateAsync(stream, "Demo", "1.0.0");

        Assert.False(result.IsValid);
        Assert.Contains("must not use source 'path'", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ValidateAsync_Rejects_Missing_Structured_Api_Doc()
    {
        var bytes = BpkTestArtifactBuilder.CreateValidArtifact(
            "Demo",
            "1.0.0",
            includeStructuredApiDoc: false);
        await using var stream = new MemoryStream(bytes);

        var result = await _validator.ValidateAsync(stream, "Demo", "1.0.0");

        Assert.False(result.IsValid);
        Assert.Contains("api.json", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ValidateAsync_Accepts_Template_Artifact_Without_Api_Json()
    {
        var bytes = BpkTestArtifactBuilder.CreateValidTemplateArtifact("beskid.templates.demo", "1.0.0");
        await using var stream = new MemoryStream(bytes);

        var result = await _validator.ValidateAsync(stream, "beskid.templates.demo", "1.0.0");

        Assert.True(result.IsValid, result.Message);
        Assert.Contains("\"packageKind\":\"template\"", result.ManifestJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ValidateAsync_Rejects_Template_Missing_Template_Json()
    {
        var packageJson = JsonSerializer.Serialize(new
        {
            schema = "beskid.package.v1",
            id = "beskid.templates.demo",
            version = "1.0.0",
            packageKind = "template",
        });
        var bytes = BpkTestArtifactBuilder.CreateValidArtifact(
            "beskid.templates.demo",
            "1.0.0",
            packageJsonOverride: packageJson,
            includeStructuredApiDoc: false);
        await using var stream = new MemoryStream(bytes);

        var result = await _validator.ValidateAsync(stream, "beskid.templates.demo", "1.0.0");

        Assert.False(result.IsValid);
        Assert.Contains("template.json", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ValidateAsync_Rejects_Template_With_Api_Json_Documentation()
    {
        var packageJson = JsonSerializer.Serialize(new
        {
            schema = "beskid.package.v1",
            id = "beskid.templates.demo",
            version = "1.0.0",
            packageKind = "template",
            documentation = new { apiJson = ".beskid/docs/api.json" },
        });
        var bytes = BpkTestArtifactBuilder.CreateValidTemplateArtifact(
            "beskid.templates.demo",
            "1.0.0",
            packageJsonOverride: packageJson);
        await using var stream = new MemoryStream(bytes);

        var result = await _validator.ValidateAsync(stream, "beskid.templates.demo", "1.0.0");

        Assert.False(result.IsValid);
        Assert.Contains("documentation.apiJson", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ValidateAsync_WhenRelaxPackageJsonVersion_Skips_Version_Equality()
    {
        var bytes = BpkTestArtifactBuilder.CreateValidArtifact("Demo", "1.0.0");
        await using var stream = new MemoryStream(bytes);

        var result = await _validator.ValidateAsync(stream, "Demo", "9.9.9", relaxPackageJsonVersion: true);

        Assert.True(result.IsValid, result.Message);
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
            [".beskid/docs/api.json"] = Encoding.UTF8.GetBytes(BpkTestArtifactBuilder.MinimalStructuredApiJson),
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

    private static Dictionary<string, byte[]> ReadZipEntries(byte[] zipBytes)
    {
        var entries = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        using var input = new MemoryStream(zipBytes);
        using var zip = new ZipArchive(input, ZipArchiveMode.Read);
        foreach (var entry in zip.Entries)
        {
            if (string.IsNullOrEmpty(entry.Name))
            {
                continue;
            }

            using var stream = entry.Open();
            using var memory = new MemoryStream();
            stream.CopyTo(memory);
            entries[entry.FullName.Replace('\\', '/')] = memory.ToArray();
        }

        return entries;
    }

    private static byte[] RecalculateChecksums(IReadOnlyDictionary<string, byte[]> entries)
    {
        var checksumLines = entries
            .Where(kv => !string.Equals(kv.Key, "checksums.sha256", StringComparison.Ordinal))
            .OrderBy(kv => kv.Key, StringComparer.Ordinal)
            .Select(kv => $"{Sha256(kv.Value)}  {kv.Key}");
        return Encoding.UTF8.GetBytes(string.Join('\n', checksumLines) + "\n");
    }

    private static string Sha256(byte[] bytes)
    {
        using var sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(bytes)).ToLowerInvariant();
    }

    [Fact]
    public void StructuredApiDocValidator_Rejects_Flat_Graph_With_Too_Many_Roots()
    {
        var items = Enumerable.Range(1, 200)
            .Select(i => $"{{\"id\":{i},\"qualifiedName\":\"S{i}\",\"name\":\"S{i}\",\"kind\":\"type\",\"parentId\":null}}");
        var json = "{\"schemaVersion\":4,\"navigationModel\":\"graph-v1\",\"items\":["
            + string.Join(',', items)
            + "]}";

        var (isValid, message) = StructuredApiDocValidator.ValidateJson(json);

        Assert.False(isValid);
        Assert.Contains("graph roots", message, StringComparison.OrdinalIgnoreCase);
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

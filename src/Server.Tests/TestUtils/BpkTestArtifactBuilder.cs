using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Server.Services;

namespace Server.Tests.TestUtils;

/// <summary>
/// Builds minimal valid .bpk ZIP payloads matching server-side package artifact validation rules.
/// </summary>
public static class BpkTestArtifactBuilder
{
    /// <summary>Minimal graph-v1 payload accepted by pack validation and pckg structured docs.</summary>
    public const string MinimalStructuredApiJson = """
        {
          "schemaVersion": 3,
          "navigationModel": "graph-v1",
          "source": "test",
          "generator": "test",
          "items": [
            {
              "id": 10,
              "qualifiedName": "Demo",
              "name": "Demo",
              "kind": "module",
              "visibility": "public",
              "modulePath": ["Demo"],
              "parentId": null,
              "memberIds": [1]
            },
            {
              "id": 1,
              "qualifiedName": "Demo::App",
              "name": "App",
              "kind": "type",
              "visibility": "public",
              "modulePath": ["Demo"],
              "parentId": 10,
              "memberIds": []
            }
          ]
        }
        """;

    public static string ArtifactSha256(byte[] artifactBytes)
        => Convert.ToHexString(SHA256.HashData(artifactBytes)).ToLowerInvariant();

    /// <summary>
    /// Same layout as a valid artifact, but the first checksum line does not match file content.
    /// </summary>
    public static byte[] CreateArtifactWithBadChecksum(string packageName, string version)
    {
        var files = new OrderedDictionary();

        var projectProj = $"name = \"{packageName}\"\n";
        files["Project.proj"] = Encoding.UTF8.GetBytes(projectProj);
        files["src/entry.bsk"] = Encoding.UTF8.GetBytes("// test entry");
        files[".beskid/docs/api.json"] = Encoding.UTF8.GetBytes(MinimalStructuredApiJson);

        var packageJson = $$"""{"schema":"beskid.package.v1","id":"{{packageName}}","version":"{{version}}"}""";
        files["package.json"] = Encoding.UTF8.GetBytes(packageJson);

        var checksumLines = new List<string>();
        var first = true;
        foreach (var kv in files)
        {
            var digest = Sha256Hex(kv.Value);
            if (first)
            {
                digest = digest[0] == '0' ? "1" + digest[1..] : "0" + digest[1..];
                first = false;
            }

            checksumLines.Add($"{digest}  {kv.Key}");
        }

        var checksumsBody = string.Join("\n", checksumLines) + "\n";
        files["checksums.sha256"] = Encoding.UTF8.GetBytes(checksumsBody);

        var memory = new MemoryStream();
        using (var zip = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var kv in files)
            {
                var entry = zip.CreateEntry(kv.Key, CompressionLevel.Fastest);
                using var s = entry.Open();
                s.Write(kv.Value);
            }
        }

        return memory.ToArray();
    }

    public const string MinimalTemplateJson = """
        {
          "schema": "beskid.template.v1",
          "identity": "test.templates.demo::1.0.0",
          "name": "Demo Template",
          "shortName": "demo",
          "description": "Test template",
          "tags": { "type": "project" },
          "sourceName": "MyApp",
          "sources": [{ "source": "./content/", "target": "./" }]
        }
        """;

    public static byte[] CreateValidTemplateArtifact(
        string packageName,
        string version,
        IReadOnlyDictionary<string, string>? additionalTextFiles = null,
        string? packageJsonOverride = null,
        string? templateJsonOverride = null)
    {
        var templateJson = templateJsonOverride ?? MinimalTemplateJson;
        var packageJson = packageJsonOverride
            ?? JsonSerializer.Serialize(new
            {
                schema = "beskid.package.v1",
                id = packageName,
                version,
                packageKind = "template",
                template = new
                {
                    shortName = "demo",
                    tags = new { type = "project" },
                },
            });

        var extras = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [PackageTemplatePaths.TemplateJsonRelativePath] = templateJson,
        };

        if (additionalTextFiles is not null)
        {
            foreach (var kv in additionalTextFiles)
            {
                extras[kv.Key] = kv.Value;
            }
        }

        return CreateValidArtifact(
            packageName,
            version,
            extras,
            packageJson,
            includeStructuredApiDoc: false);
    }

    public static byte[] CreateValidArtifact(
        string packageName,
        string version,
        IReadOnlyDictionary<string, string>? additionalTextFiles = null,
        string? packageJsonOverride = null,
        bool includeStructuredApiDoc = true)
    {
        var files = new OrderedDictionary();

        var projectProj = $"name = \"{packageName}\"\n";
        files["Project.proj"] = Encoding.UTF8.GetBytes(projectProj);
        files["src/entry.bsk"] = Encoding.UTF8.GetBytes("// test entry");
        if (includeStructuredApiDoc)
        {
            files[".beskid/docs/api.json"] = Encoding.UTF8.GetBytes(MinimalStructuredApiJson);
        }

        if (additionalTextFiles is not null)
        {
            foreach (var kv in additionalTextFiles)
            {
                var key = kv.Key.Replace('\\', '/').TrimStart('/');
                files[key] = Encoding.UTF8.GetBytes(kv.Value);
            }
        }

        var packageJson = packageJsonOverride
            ?? JsonSerializer.Serialize(new
            {
                schema = "beskid.package.v1",
                id = packageName,
                version,
                documentation = includeStructuredApiDoc
                    ? new { apiJson = ".beskid/docs/api.json", schemaVersion = 4 }
                    : null,
            });
        files["package.json"] = Encoding.UTF8.GetBytes(packageJson);

        var checksumLines = new List<string>();
        foreach (var kv in files)
        {
            checksumLines.Add($"{Sha256Hex(kv.Value)}  {kv.Key}");
        }

        var checksumsBody = string.Join("\n", checksumLines) + "\n";
        files["checksums.sha256"] = Encoding.UTF8.GetBytes(checksumsBody);

        var memory = new MemoryStream();
        using (var zip = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var kv in files)
            {
                var entry = zip.CreateEntry(kv.Key, CompressionLevel.Fastest);
                using var s = entry.Open();
                s.Write(kv.Value);
            }
        }

        return memory.ToArray();
    }

    private static string Sha256Hex(byte[] bytes)
    {
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>
    /// Preserves insertion order for deterministic checksums lines.
    /// </summary>
    private sealed class OrderedDictionary : IEnumerable<KeyValuePair<string, byte[]>>
    {
        private readonly List<KeyValuePair<string, byte[]>> _items = [];

        public byte[] this[string key]
        {
            set
            {
                for (var i = 0; i < _items.Count; i++)
                {
                    if (string.Equals(_items[i].Key, key, StringComparison.OrdinalIgnoreCase))
                    {
                        _items[i] = new KeyValuePair<string, byte[]>(key, value);
                        return;
                    }
                }

                _items.Add(new KeyValuePair<string, byte[]>(key, value));
            }
        }

        public IEnumerator<KeyValuePair<string, byte[]>> GetEnumerator() => _items.GetEnumerator();

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}

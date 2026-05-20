using Server.Services;

namespace Server.Tests.Unit;

public sealed class PackageManifestMetadataReaderTests
{
    [Fact]
    public void Read_resolves_documentation_readme_path_and_manifest_objects()
    {
        const string json = """
            {
              "schema": "beskid.package.v1",
              "id": "demo",
              "version": "1.0.0",
              "documentation": { "readme": "docs/guide.md" },
              "configuration": { "a": 1 },
              "overrides": { "b": true },
              "iconUrl": "https://example.com/icon.svg"
            }
            """;

        var metadata = PackageManifestMetadataReader.Read(json);

        Assert.Equal("docs/guide.md", metadata.ReadmePath);
        Assert.Contains("\"a\"", metadata.ConfigurationJson, StringComparison.Ordinal);
        Assert.Contains("\"b\"", metadata.OverridesJson, StringComparison.Ordinal);
        Assert.Equal("https://example.com/icon.svg", metadata.IconUrl);
    }
}

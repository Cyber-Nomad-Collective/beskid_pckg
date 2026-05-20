using System.Text;
using Server.Services;
using Server.Tests.TestUtils;

namespace Server.Tests.Unit;

public sealed class PackageArtifactPublishMetadataExtractorTests
{
    private readonly PackageArtifactPublishMetadataExtractor _extractor = new();

    [Fact]
    public void Extract_reads_readme_from_manifest_path()
    {
        const string packageName = "Readme.Path";
        const string version = "1.0.0";
        var packageJson = $$"""
            {
              "schema": "beskid.package.v1",
              "id": "{{packageName}}",
              "version": "{{version}}",
              "documentation": { "readme": "docs/overview.md" }
            }
            """;

        var artifact = BpkTestArtifactBuilder.CreateValidArtifact(
            packageName,
            version,
            new Dictionary<string, string>
            {
                ["docs/overview.md"] = "# Overview\n\nHello from docs.",
            },
            packageJson);

        using var stream = new MemoryStream(artifact);
        var metadata = _extractor.Extract(stream, packageJson);

        Assert.Equal("# Overview\n\nHello from docs.", metadata.ReadmeMarkdown);
    }

    [Fact]
    public void Extract_reads_configuration_and_overrides_from_manifest()
    {
        const string packageName = "Config.Demo";
        const string version = "2.0.0";
        var packageJson = $$"""
            {
              "schema": "beskid.package.v1",
              "id": "{{packageName}}",
              "version": "{{version}}",
              "configuration": { "targetFramework": "net10.0" },
              "overrides": { "featureFlags": { "beta": true } },
              "iconUrl": "https://cdn.example.com/icon.png"
            }
            """;

        var artifact = BpkTestArtifactBuilder.CreateValidArtifact(packageName, version, packageJsonOverride: packageJson);
        using var stream = new MemoryStream(artifact);
        var metadata = _extractor.Extract(stream, packageJson);

        Assert.Contains("targetFramework", metadata.ConfigurationJson, StringComparison.Ordinal);
        Assert.Contains("featureFlags", metadata.OverridesJson, StringComparison.Ordinal);
        Assert.Equal("https://cdn.example.com/icon.png", metadata.IconUrl);
    }

    [Fact]
    public void Extract_falls_back_to_root_readme_when_manifest_has_no_pointer()
    {
        const string packageName = "Readme.Default";
        const string version = "1.0.0";
        var packageJson = $$"""{"schema":"beskid.package.v1","id":"{{packageName}}","version":"{{version}}"}""";

        var artifact = BpkTestArtifactBuilder.CreateValidArtifact(
            packageName,
            version,
            new Dictionary<string, string> { ["README.md"] = "# Root readme" },
            packageJson);

        using var stream = new MemoryStream(artifact);
        var metadata = _extractor.Extract(stream, packageJson);

        Assert.Equal("# Root readme", metadata.ReadmeMarkdown);
    }
}

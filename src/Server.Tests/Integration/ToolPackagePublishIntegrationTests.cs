using System.Net;
using System.Net.Http.Json;
using Server.Services;
using Server.Tests.TestUtils;

namespace Server.Tests.Integration;

public sealed class ToolPackagePublishIntegrationTests : IClassFixture<TestApplicationFactory>
{
    private readonly TestApplicationFactory _factory;

    public ToolPackagePublishIntegrationTests(TestApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Publish_Accepts_Tool_Artifact_Without_Api_Json()
    {
        var (_, apiKey, package) = await _factory.SeedOwnerWithPackageAsync(
            "beskid.tools.integration",
            isPublic: true);
        var artifact = BpkTestArtifactBuilder.CreateValidToolArtifact(package.Name, "1.0.0");
        var digest = BpkTestArtifactBuilder.ArtifactSha256(artifact);

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-API-Key", apiKey);
        using var publishForm = new MultipartFormDataContent
        {
            { new StringContent("1.0.0"), "version" },
            { new StringContent(digest), "checksumSha256" },
            { new ByteArrayContent(artifact), "artifact", "tool.bpk" },
        };

        var publish = await client.PostAsync($"/api/packages/{package.Name}/publish", publishForm);
        var body = await publish.Content.ReadAsStringAsync();

        Assert.True(
            publish.StatusCode == HttpStatusCode.OK,
            $"Expected OK but got {publish.StatusCode}: {body}");

        var version = await _factory.GetPackageVersionAsync(package.Name, "1.0.0");
        Assert.NotNull(version);
        var metadata = PackageManifestMetadataReader.Read(version!.ManifestJson);
        Assert.Equal(PackageKinds.Tool, metadata.PackageKind);
    }
}

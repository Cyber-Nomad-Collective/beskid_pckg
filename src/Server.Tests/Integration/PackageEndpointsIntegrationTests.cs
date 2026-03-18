using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Server.Tests.TestUtils;

namespace Server.Tests.Integration;

public class PackageEndpointsIntegrationTests : IClassFixture<TestApplicationFactory>
{
    private readonly TestApplicationFactory _factory;

    public PackageEndpointsIntegrationTests(TestApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetPackages_Returns_Public_Packages_Without_Auth()
    {
        var (_, _, package) = await _factory.SeedOwnerWithPackageAsync("Public.Demo", isPublic: true);
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/packages");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadAsStringAsync();
        Assert.Contains(package.Name, payload);
    }

    [Fact]
    public async Task PublishEndpoint_Rejects_Invalid_Artifact()
    {
        var (_, apiKey, package) = await _factory.SeedOwnerWithPackageAsync("Bad.Artifact", isPublic: true);
        var client = _factory.CreateClient();

        using var form = new MultipartFormDataContent
        {
            { new StringContent("1.0.0"), "version" },
            { new ByteArrayContent([1, 2, 3, 4]), "artifact", "bad.bpk" },
        };
        client.DefaultRequestHeaders.Add("X-API-Key", apiKey);

        var response = await client.PostAsync($"/api/packages/{package.Name}/publish", form);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("ZIP", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"success\":false", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UpsertPackage_Requires_Authentication()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/packages", new
        {
            Name = "Auth.Required.Demo",
            Description = "test",
            RepositoryUrl = (string?)null,
            WebsiteUrl = (string?)null,
            IsPublic = true,
            SubmitForReview = false,
            ReviewReason = (string?)null,
        });

        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task UpsertPackage_Allows_ApiKey_Authentication()
    {
        var (_, apiKey, package) = await _factory.SeedOwnerWithPackageAsync("Managed.Demo", isPublic: true);
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-API-Key", apiKey);

        var response = await client.PostAsJsonAsync("/api/packages", new
        {
            Name = package.Name,
            Description = "updated description",
            RepositoryUrl = "https://example.com/repo",
            WebsiteUrl = "https://example.com",
            IsPublic = true,
            SubmitForReview = false,
            ReviewReason = (string?)null,
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"success\":true", payload, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Publish_ListVersions_And_Download_RoundTrip_Succeeds()
    {
        var (_, apiKey, package) = await _factory.SeedOwnerWithPackageAsync("RoundTrip.Demo", isPublic: true);
        var artifact = BpkTestArtifactBuilder.CreateValidArtifact(package.Name, "1.0.0");
        var digest = BpkTestArtifactBuilder.ArtifactSha256(artifact);

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-API-Key", apiKey);

        using var publishForm = new MultipartFormDataContent
        {
            { new StringContent("1.0.0"), "version" },
            { new StringContent(digest), "checksumSha256" },
            { new ByteArrayContent(artifact), "artifact", "roundtrip.bpk" },
        };

        var publish = await client.PostAsync($"/api/packages/{package.Name}/publish", publishForm);
        Assert.Equal(HttpStatusCode.OK, publish.StatusCode);

        var listed = await client.GetAsync($"/api/packages/{package.Name}/versions");
        Assert.Equal(HttpStatusCode.OK, listed.StatusCode);
        var listedJson = await listed.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(listedJson.ValueKind == JsonValueKind.Array);
        Assert.Contains(listedJson.EnumerateArray(), v =>
            v.TryGetProperty("version", out var versionProp)
            && versionProp.GetString() == "1.0.0");

        var downloaded = await client.GetByteArrayAsync($"/api/packages/{package.Name}/versions/1.0.0/download");
        Assert.Equal(artifact, downloaded);

        var persisted = await _factory.GetPackageVersionAsync(package.Name, "1.0.0");
        Assert.NotNull(persisted);
        Assert.Equal(digest, persisted!.ChecksumSha256);
    }
}

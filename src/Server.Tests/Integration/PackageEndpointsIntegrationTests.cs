using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
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
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

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

    [Fact]
    public async Task SearchEndpoint_Returns_Package_By_Query()
    {
        var (_, _, package) = await _factory.SeedOwnerWithPackageAsync("Search.Demo", isPublic: true);
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/api/search?q={package.Name}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadAsStringAsync();
        Assert.Contains(package.Name, payload, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PackageDetails_Can_Be_Fetched_By_Name()
    {
        var (_, _, package) = await _factory.SeedOwnerWithPackageAsync("Details.Demo", isPublic: true);
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/api/packages/{package.Name}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadAsStringAsync();
        Assert.Contains(package.Name, payload, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Yank_And_Unyank_Controls_Download_Availability()
    {
        var (_, apiKey, package) = await _factory.SeedOwnerWithPackageAsync("Yank.Demo", isPublic: true);
        var artifact = BpkTestArtifactBuilder.CreateValidArtifact(package.Name, "1.0.0");

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-API-Key", apiKey);

        using var publishForm = new MultipartFormDataContent
        {
            { new StringContent("1.0.0"), "version" },
            { new ByteArrayContent(artifact), "artifact", "yank-demo.bpk" },
        };
        var publishResponse = await client.PostAsync($"/api/packages/{package.Name}/publish", publishForm);
        Assert.Equal(HttpStatusCode.OK, publishResponse.StatusCode);

        var yankResponse = await client.PostAsync($"/api/packages/{package.Name}/versions/1.0.0/yank", content: null);
        Assert.Equal(HttpStatusCode.OK, yankResponse.StatusCode);

        var yankedDownload = await client.GetAsync($"/api/packages/{package.Name}/versions/1.0.0/download");
        Assert.Equal(HttpStatusCode.NotFound, yankedDownload.StatusCode);

        var unyankResponse = await client.PostAsync($"/api/packages/{package.Name}/versions/1.0.0/unyank", content: null);
        Assert.Equal(HttpStatusCode.OK, unyankResponse.StatusCode);

        var restoredDownload = await client.GetAsync($"/api/packages/{package.Name}/versions/1.0.0/download");
        Assert.Equal(HttpStatusCode.OK, restoredDownload.StatusCode);
    }
}

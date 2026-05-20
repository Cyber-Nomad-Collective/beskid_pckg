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

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
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
            Category = "General",
            RepositoryUrl = (string?)null,
            WebsiteUrl = (string?)null,
            Tags = Array.Empty<string>(),
            IsPublic = true,
            SubmitForReview = false,
            ReviewReason = (string?)null,
            iconUrl = (string?)null,
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
            Category = "General",
            RepositoryUrl = "https://example.com/repo",
            WebsiteUrl = "https://example.com",
            Tags = Array.Empty<string>(),
            IsPublic = true,
            SubmitForReview = false,
            ReviewReason = (string?)null,
            iconUrl = (string?)null,
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
    public async Task Publish_Persists_Readme_And_Manifest_Metadata_In_Package_Details()
    {
        const string version = "1.0.0";
        var (_, apiKey, package) = await _factory.SeedOwnerWithPackageAsync("Ingest.Readme", isPublic: true);
        var packageJson = $$"""
            {
              "schema": "beskid.package.v1",
              "id": "{{package.Name}}",
              "version": "{{version}}",
              "documentation": { "readme": "README.md" },
              "configuration": { "profile": "release" },
              "overrides": { "strict": true }
            }
            """;
        var artifact = BpkTestArtifactBuilder.CreateValidArtifact(
            package.Name,
            version,
            new Dictionary<string, string> { ["README.md"] = "# Hello from artifact README" },
            packageJson);

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-API-Key", apiKey);

        using var publishForm = new MultipartFormDataContent
        {
            { new StringContent(version), "version" },
            { new ByteArrayContent(artifact), "artifact", "readme-ingest.bpk" },
        };
        var publish = await client.PostAsync($"/api/packages/{package.Name}/publish", publishForm);
        Assert.Equal(HttpStatusCode.OK, publish.StatusCode);

        var persisted = await _factory.GetPackageVersionAsync(package.Name, version);
        Assert.NotNull(persisted);
        Assert.Equal("# Hello from artifact README", persisted!.ReadmeMarkdown);
        Assert.Contains("profile", persisted.ConfigurationJson, StringComparison.Ordinal);
        Assert.Contains("strict", persisted.OverridesJson, StringComparison.Ordinal);

        var details = await client.GetFromJsonAsync<JsonElement>($"/api/packages/{package.Name}");
        Assert.Equal("# Hello from artifact README", details.GetProperty("readme").GetString());
        Assert.Contains("profile", details.GetProperty("configuration").GetRawText(), StringComparison.Ordinal);
        Assert.Contains("strict", details.GetProperty("overrides").GetRawText(), StringComparison.Ordinal);

        var versions = details.GetProperty("versions").EnumerateArray().ToList();
        Assert.Single(versions);
        Assert.True(versions[0].GetProperty("hasReadme").GetBoolean());
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

    [Fact]
    public async Task DeletePackage_Requires_Authentication()
    {
        var (_, _, package) = await _factory.SeedOwnerWithPackageAsync("Delete.Auth", isPublic: true);
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.DeleteAsync($"/api/packages/{Uri.EscapeDataString(package.Name)}");

        // Cookie auth challenges typically redirect to the login page instead of returning 401.
        Assert.True(
            response.StatusCode == HttpStatusCode.Unauthorized
            || response.StatusCode == HttpStatusCode.Redirect
            || response.StatusCode == HttpStatusCode.Found);
    }

    [Fact]
    public async Task DeletePackage_AsOwner_Succeeds()
    {
        var (user, _, package) = await _factory.SeedOwnerWithPackageAsync("Delete.Owner", isPublic: true);
        var client = await _factory.CreateAuthenticatedPublisherClientAsync(user);

        var response = await client.DeleteAsync($"/api/packages/{Uri.EscapeDataString(package.Name)}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"success\":true", body, StringComparison.OrdinalIgnoreCase);

        var anon = _factory.CreateClient();
        var get = await anon.GetAsync($"/api/packages/{Uri.EscapeDataString(package.Name)}");
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
    }

    [Fact]
    public async Task DeletePackage_AsSuperAdmin_Succeeds()
    {
        var (_, _, package) = await _factory.SeedOwnerWithPackageAsync("Delete.Admin", isPublic: true);
        var admin = await _factory.CreateAuthenticatedSuperAdminClientAsync();

        var response = await admin.DeleteAsync($"/api/packages/{Uri.EscapeDataString(package.Name)}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"success\":true", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DeletePackage_AsOtherPublisher_Returns_Forbidden()
    {
        var (_, _, victim) = await _factory.SeedOwnerWithPackageAsync("Delete.Victim", isPublic: true);
        var (otherUser, _, _) = await _factory.SeedOwnerWithPackageAsync("Delete.Attacker", isPublic: true);
        var otherClient = await _factory.CreateAuthenticatedPublisherClientAsync(otherUser);

        var response = await otherClient.DeleteAsync($"/api/packages/{Uri.EscapeDataString(victim.Name)}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}

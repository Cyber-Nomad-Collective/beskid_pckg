using System.Net;
using Server.Tests.TestUtils;

namespace Server.Tests.Integration;

public class RegistryActivityIntegrationTests : IClassFixture<TestApplicationFactory>
{
    private readonly TestApplicationFactory _factory;

    public RegistryActivityIntegrationTests(TestApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task RegistryActivity_Endpoint_Returns_Publish_Success_Entry()
    {
        var (_, apiKey, package) = await _factory.SeedOwnerWithPackageAsync("Activity.Publish", isPublic: true);
        var artifact = BpkTestArtifactBuilder.CreateValidArtifact(package.Name, "1.0.0");

        var publisher = _factory.CreateClient();
        publisher.DefaultRequestHeaders.Add("X-API-Key", apiKey);

        using var form = new MultipartFormDataContent
        {
            { new StringContent("1.0.0"), "version" },
            { new ByteArrayContent(artifact), "artifact", "activity.bpk" },
        };

        var publish = await publisher.PostAsync($"/api/packages/{package.Name}/publish", form);
        Assert.Equal(HttpStatusCode.OK, publish.StatusCode);

        var adminClient = await _factory.CreateAuthenticatedSuperAdminClientAsync();
        var list = await adminClient.GetAsync("/api/admin/registry-activity?take=50");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);

        var json = await list.Content.ReadAsStringAsync();
        Assert.Contains("publish_success", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(package.Name, json, StringComparison.Ordinal);
    }
}

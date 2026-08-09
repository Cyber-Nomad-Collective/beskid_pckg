using System.Net;
using System.Net.Http.Json;

namespace Server.Tests.Integration;

public sealed class AuthHubPairingEndpointIntegrationTests : IClassFixture<TestApplicationFactory>
{
    private readonly TestApplicationFactory _factory;

    public AuthHubPairingEndpointIntegrationTests(TestApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Pairing_endpoints_are_available_under_the_single_api_prefix()
    {
        var client = _factory.CreateClient();
        var admin = await _factory.CreateAuthenticatedSuperAdminClientAsync();

        var status = await client.GetAsync("/api/auth/hub/pairing-status");
        var pair = await admin.PostAsJsonAsync("/api/auth/hub/pair", new
        {
            code = "test-pairing-code",
            publicUrl = "https://pckg.example.test"
        });

        Assert.Equal(HttpStatusCode.OK, status.StatusCode);
        Assert.NotEqual(HttpStatusCode.NotFound, pair.StatusCode);
    }
}

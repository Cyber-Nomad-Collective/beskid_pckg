using System.Net;

namespace Server.Tests.Integration;

public sealed class ReactStaticHostingIntegrationTests : IClassFixture<TestApplicationFactory>
{
    private readonly TestApplicationFactory _factory;

    public ReactStaticHostingIntegrationTests(TestApplicationFactory factory)
    {
        _factory = factory;
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/dashboard/profile")]
    public async Task Browser_routes_serve_the_vite_shell_without_a_blazor_bootstrap(string path)
    {
        var response = await _factory.CreateClient().GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("pckg-react-root", html, StringComparison.Ordinal);
        Assert.DoesNotContain("_framework/blazor.web.js", html, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("/api/not-a-real-endpoint")]
    [InlineData("/health/live")]
    [InlineData("/assets/missing.js")]
    public async Task Server_and_missing_asset_paths_never_fall_back_to_the_vite_shell(string path)
    {
        var response = await _factory.CreateClient().GetAsync(path);

        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("pckg-react-root", body, StringComparison.Ordinal);
    }
}

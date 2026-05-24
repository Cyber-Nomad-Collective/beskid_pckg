using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Server.Data;
using Server.Services;
using Server.Tests.TestUtils;

namespace Server.Tests.Integration;

public class WorkspacePublishIntegrationTests : IClassFixture<TestApplicationFactory>
{
    private readonly TestApplicationFactory _factory;

    public WorkspacePublishIntegrationTests(TestApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task PublishWorkspace_AutoProvisions_Missing_Packages_And_Assigns_Versions()
    {
        var suffix = Guid.NewGuid().ToString("N")[..12];
        var (_, apiKey, _) = await _factory.SeedOwnerWithPackageAsync("Workspace.AutoProvision", isPublic: true);

        var bundle = WorkspaceBundleTestBuilder.CreateTwoMemberWorkspaceBundle(suffix);
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-API-Key", apiKey);

        using var form = new MultipartFormDataContent
        {
            { new ByteArrayContent(bundle), "artifact", "workspace.bundle.zip" },
        };

        var response = await client.PostAsync("/api/workspaces/publish", form);
        var body = await response.Content.ReadAsStringAsync();

        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"Expected OK but got {response.StatusCode}: {body}");
        Assert.Contains("\"success\":true", body, StringComparison.OrdinalIgnoreCase);

        var foundationVersion = await _factory.GetPackageVersionAsync($"Pkg.Foundation.{suffix}", "0.0.1");
        var consumerVersion = await _factory.GetPackageVersionAsync($"Pkg.Consumer.{suffix}", "0.0.1");
        Assert.NotNull(foundationVersion);
        Assert.NotNull(consumerVersion);
    }

    [Fact]
    public async Task PublishWorkspace_Replaces_Internal_Path_Dependencies_And_Assigns_Versions()
    {
        var suffix = Guid.NewGuid().ToString("N")[..12];
        var foundationPackage = $"Pkg.Foundation.{suffix}";
        var consumerPackage = $"Pkg.Consumer.{suffix}";
        var (user, apiKey, _) = await _factory.SeedOwnerWithPackageAsync("Workspace.Seed", isPublic: true);
        await SeedPackageAsync(user.Id, foundationPackage);
        await SeedPackageAsync(user.Id, consumerPackage);

        var bundle = WorkspaceBundleTestBuilder.CreateTwoMemberWorkspaceBundle(suffix);
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-API-Key", apiKey);

        using var form = new MultipartFormDataContent
        {
            { new ByteArrayContent(bundle), "artifact", "workspace.bundle.zip" },
        };

        var response = await client.PostAsync("/api/workspaces/publish", form);
        var body = await response.Content.ReadAsStringAsync();

        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"Expected OK but got {response.StatusCode}: {body}");
        Assert.Contains("\"success\":true", body, StringComparison.OrdinalIgnoreCase);

        using var doc = JsonDocument.Parse(body);
        var packages = doc.RootElement.GetProperty("packages");
        Assert.Equal(2, packages.GetArrayLength());

        var foundationVersion = await _factory.GetPackageVersionAsync(foundationPackage, "0.0.1");
        var consumerVersion = await _factory.GetPackageVersionAsync(consumerPackage, "0.0.1");
        Assert.NotNull(foundationVersion);
        Assert.NotNull(consumerVersion);

        var consumerManifest = PackageManifestMetadataReader.Read(consumerVersion!.ManifestJson);
        var foundationDependency = Assert.Single(consumerManifest.Dependencies);
        Assert.Equal(foundationPackage, foundationDependency.Name);
        Assert.Equal("registry", foundationDependency.Source);
        Assert.Equal("0.0.1", foundationDependency.Version);
    }

    private async Task SeedPackageAsync(string ownerUserId, string packageName)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        if (await db.Packages.AnyAsync(p => p.Name == packageName))
        {
            return;
        }

        db.Packages.Add(new PackageEntity
        {
            Id = Guid.NewGuid(),
            OwnerUserId = ownerUserId,
            Name = packageName,
            Description = "workspace publish test",
            IsPublic = true,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();
    }
}

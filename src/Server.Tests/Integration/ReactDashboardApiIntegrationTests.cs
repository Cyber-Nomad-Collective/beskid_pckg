using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Server.Data;

namespace Server.Tests.Integration;

public sealed class ReactDashboardApiIntegrationTests : IClassFixture<TestApplicationFactory>
{
    private readonly TestApplicationFactory _factory;

    public ReactDashboardApiIntegrationTests(TestApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Api_key_dashboard_contract_lists_creates_and_revokes_the_current_users_keys()
    {
        var (user, _, _) = await _factory.SeedOwnerWithPackageAsync("React.ApiKeys");
        var client = await _factory.CreateAuthenticatedPublisherClientAsync(user);

        var listed = await client.GetAsync("/api/api-keys");
        Assert.Equal(HttpStatusCode.OK, listed.StatusCode);
        Assert.Contains("default", await listed.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        var created = await client.PostAsJsonAsync("/api/api-keys", new { name = "dashboard", scopes = new[] { "read" } });
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var createdJson = await created.Content.ReadFromJsonAsync<ApiKeyCreatedContract>();
        Assert.NotNull(createdJson);
        Assert.False(string.IsNullOrWhiteSpace(createdJson.PlainTextKey));
        Assert.Equal("dashboard", createdJson.Key.Name);

        var revoked = await client.DeleteAsync($"/api/api-keys/{createdJson.Key.Id}");
        Assert.Equal(HttpStatusCode.NoContent, revoked.StatusCode);
    }

    [Fact]
    public async Task Admin_dashboard_contract_lists_updates_users_and_manages_permissions()
    {
        var (target, _, _) = await _factory.SeedOwnerWithPackageAsync("React.Admin");
        var admin = await _factory.CreateAuthenticatedSuperAdminClientAsync();

        var listed = await admin.GetAsync("/api/admin/users");
        Assert.Equal(HttpStatusCode.OK, listed.StatusCode);
        Assert.Contains(target.Id, await listed.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        var updated = await admin.PatchAsJsonAsync(
            $"/api/admin/users/{Uri.EscapeDataString(target.Id)}",
            new { roles = new[] { "Moderator" }, publisherVerified = true });
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        Assert.Contains("Moderator", await updated.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        var granted = await admin.PostAsJsonAsync("/api/admin/permissions", new
        {
            subject = target.Id,
            resource = "package:beskid.dashboard",
            capability = "Moderate"
        });
        Assert.Equal(HttpStatusCode.Created, granted.StatusCode);

        var permissions = await admin.GetAsync("/api/admin/permissions");
        Assert.Equal(HttpStatusCode.OK, permissions.StatusCode);
        Assert.Contains("beskid.dashboard", await permissions.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Community_dashboard_contract_reads_and_updates_profile_and_notifications()
    {
        var (user, _, _) = await _factory.SeedOwnerWithPackageAsync("React.Community");
        var client = await _factory.CreateAuthenticatedPublisherClientAsync(user);
        var notificationId = Guid.NewGuid();
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Notifications.Add(new NotificationEntity
            {
                Id = notificationId,
                UserId = user.Id,
                Type = NotificationType.System,
                Title = "Dashboard test",
                Message = "React contract",
                CreatedAtUtc = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync();
        }

        var profile = await client.GetAsync($"/api/community/profiles/{Uri.EscapeDataString(user.Id)}");
        Assert.Equal(HttpStatusCode.OK, profile.StatusCode);
        Assert.Contains(user.Id, await profile.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        var updateProfile = await client.PutAsJsonAsync("/api/community/profiles/me", new
        {
            displayName = "React Publisher",
            bio = "Dashboard profile",
            socialLinks = new[] { "https://example.test/react" }
        });
        Assert.Equal(HttpStatusCode.OK, updateProfile.StatusCode);
        Assert.Contains("React Publisher", await updateProfile.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        var notifications = await client.GetAsync("/api/community/notifications");
        Assert.Equal(HttpStatusCode.OK, notifications.StatusCode);
        Assert.Contains(notificationId.ToString(), await notifications.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        var preference = await client.PutAsJsonAsync("/api/community/notification-preferences", new { mode = "mentionsOnly" });
        Assert.Equal(HttpStatusCode.NoContent, preference.StatusCode);

        var markRead = await client.PostAsync($"/api/community/notifications/{notificationId}/read", null);
        Assert.Equal(HttpStatusCode.NoContent, markRead.StatusCode);

        await using var verifyScope = _factory.Services.CreateAsyncScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.True((await verifyDb.Notifications.SingleAsync(x => x.Id == notificationId)).IsRead);
    }

    private sealed record ApiKeyCreatedContract(ApiKeyContract Key, string PlainTextKey);
    private sealed record ApiKeyContract(Guid Id, string Name);
}

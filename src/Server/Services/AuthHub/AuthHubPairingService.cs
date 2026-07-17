using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Server.Data;

namespace Server.Services.AuthHub;

public sealed class AuthHubPairingService : IAuthHubPairingService
{
    private readonly ApplicationDbContext _db;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IDataProtector _protector;
    private readonly AuthHubPairingOptions _options;

    public AuthHubPairingService(
        ApplicationDbContext db,
        IHttpClientFactory httpClientFactory,
        IDataProtectionProvider dataProtectionProvider,
        IOptions<AuthHubPairingOptions> options)
    {
        _db = db;
        _httpClientFactory = httpClientFactory;
        _protector = dataProtectionProvider.CreateProtector("AuthHub.ServiceToken");
        _options = options.Value;
    }

    public async Task<AuthHubPairingStatus> GetStatusAsync(CancellationToken ct = default)
    {
        var settings = await _db.AuthHubSettings.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == 1, ct);
        var paired = !string.IsNullOrWhiteSpace(settings?.ProtectedServiceToken);
        var defaultPublicUrl = ResolvePublicUrl()?.Trim().TrimEnd('/') ?? string.Empty;
        var discovery = await DiscoverAsync(ct);
        return new AuthHubPairingStatus(
            paired,
            defaultPublicUrl,
            discovery.HubAvailable,
            discovery.AppRegistered);
    }

    private async Task<(bool HubAvailable, bool AppRegistered)> DiscoverAsync(CancellationToken ct)
    {
        var hubUrl = ResolveHubUrl();
        if (hubUrl is null)
        {
            return (false, false);
        }

        try
        {
            var client = _httpClientFactory.CreateClient(nameof(AuthHubPairingService));
            using var health = await client.GetAsync($"{hubUrl}/api/v1/health", ct);
            if (!health.IsSuccessStatusCode)
            {
                return (false, false);
            }

            using var status = await client.GetAsync(
                $"{hubUrl}/api/v1/pairing/status?appId=pckg", ct);
            if (!status.IsSuccessStatusCode)
            {
                return (true, false);
            }

            var body = await status.Content.ReadFromJsonAsync<PairingStatusResponse>(cancellationToken: ct);
            return (true, body?.AppId is "pckg");
        }
        catch (HttpRequestException)
        {
            return (false, false);
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            return (false, false);
        }
    }

    public async Task<AuthHubPairingResult> CompletePairingAsync(
        string code,
        string publicUrl,
        CancellationToken ct = default)
    {
        code = code.Trim();
        publicUrl = publicUrl.Trim().TrimEnd('/');
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(publicUrl))
        {
            return new AuthHubPairingResult(false, "Pairing code and public URL are required.");
        }

        if (await IsPairedAsync(ct))
        {
            return new AuthHubPairingResult(true, AlreadyPaired: true);
        }

        var hubUrl = ResolveHubUrl();
        if (hubUrl is null)
        {
            return new AuthHubPairingResult(false, "AUTH_HUB_PUBLIC_URL is not configured.");
        }

        var approverLogin = await ResolveApproverLoginAsync(ct);
        if (approverLogin is null)
        {
            return new AuthHubPairingResult(
                false,
                "Configure GITHUB_SYNC_TOKEN or PCKG_PAIRING_APPROVER_LOGIN to approve pairing.");
        }

        var client = _httpClientFactory.CreateClient(nameof(AuthHubPairingService));
        using var response = await client.PostAsJsonAsync(
            $"{hubUrl}/api/v1/pairing/approve",
            new ApprovePairingRequest(code, "pckg", publicUrl, approverLogin),
            ct);

        if (!response.IsSuccessStatusCode)
        {
            var error = await ReadErrorAsync(response, ct);
            return new AuthHubPairingResult(false, error);
        }

        var body = await response.Content.ReadFromJsonAsync<ApprovePairingResponse>(cancellationToken: ct);
        if (string.IsNullOrWhiteSpace(body?.ServiceToken))
        {
            return new AuthHubPairingResult(false, "Auth hub did not return a service token.");
        }

        await SavePairingAsync(hubUrl, body.ServiceToken, ct);
        return new AuthHubPairingResult(true);
    }

    private async Task<bool> IsPairedAsync(CancellationToken ct)
    {
        var settings = await _db.AuthHubSettings.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == 1, ct);
        return !string.IsNullOrWhiteSpace(settings?.ProtectedServiceToken);
    }

    private string? ResolveHubUrl()
    {
        var configured = _options.HubPublicUrl?.Trim().TrimEnd('/');
        return string.IsNullOrWhiteSpace(configured) ? null : configured;
    }

    private string? ResolvePublicUrl()
    {
        var configured = _options.PublicUrl?.Trim().TrimEnd('/');
        return string.IsNullOrWhiteSpace(configured) ? null : configured;
    }

    private async Task<string?> ResolveApproverLoginAsync(CancellationToken ct)
    {
        var syncToken = _options.GitHubSyncToken?.Trim();
        if (!string.IsNullOrWhiteSpace(syncToken))
        {
            var client = _httpClientFactory.CreateClient(nameof(AuthHubPairingService));
            using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.github.com/user");
            request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {syncToken}");
            request.Headers.TryAddWithoutValidation("User-Agent", "beskid-pckg");
            request.Headers.TryAddWithoutValidation("Accept", "application/vnd.github+json");

            using var response = await client.SendAsync(request, ct);
            if (response.IsSuccessStatusCode)
            {
                var user = await response.Content.ReadFromJsonAsync<GitHubUserResponse>(cancellationToken: ct);
                if (!string.IsNullOrWhiteSpace(user?.Login))
                {
                    return user.Login;
                }
            }
        }

        var configured = _options.PairingApproverLogin?.Trim();
        return string.IsNullOrWhiteSpace(configured) ? null : configured;
    }

    private async Task SavePairingAsync(string hubUrl, string serviceToken, CancellationToken ct)
    {
        var settings = await _db.AuthHubSettings.FirstOrDefaultAsync(x => x.Id == 1, ct);
        if (settings is null)
        {
            settings = new AuthHubSettingsEntity { Id = 1 };
            await _db.AuthHubSettings.AddAsync(settings, ct);
        }

        settings.HubUrl = hubUrl;
        settings.ProtectedServiceToken = _protector.Protect(serviceToken);
        settings.PairedAtUtc = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    private static async Task<string> ReadErrorAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            var body = await response.Content.ReadFromJsonAsync<ErrorResponse>(cancellationToken: ct);
            if (!string.IsNullOrWhiteSpace(body?.Error))
            {
                return body.Error;
            }
        }
        catch
        {
            // fall through
        }

        return $"Pairing failed (HTTP {(int)response.StatusCode}).";
    }

    private sealed record ApprovePairingRequest(
        string Code,
        string AppId,
        string PublicUrl,
        string ApproverLogin);

    private sealed record ApprovePairingResponse(
        [property: JsonPropertyName("serviceToken")] string? ServiceToken);

    private sealed record ErrorResponse(
        [property: JsonPropertyName("error")] string? Error);

    private sealed record PairingStatusResponse(
        [property: JsonPropertyName("appId")] string? AppId,
        [property: JsonPropertyName("paired")] bool Paired);

    private sealed record GitHubUserResponse(
        [property: JsonPropertyName("login")] string? Login);
}

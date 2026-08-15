namespace Server.Services.AuthHub;

public sealed record AuthHubPairingStatus(
    bool Paired,
    string DefaultPublicUrl,
    bool HubAvailable = false,
    bool AppRegistered = false);

public sealed record AuthHubPairingResult(bool Ok, string? Error = null, bool AlreadyPaired = false);

public interface IAuthHubPairingService
{
    Task<AuthHubPairingStatus> GetStatusAsync(CancellationToken ct = default);

    Task<AuthHubPairingResult> CompletePairingAsync(
        string code,
        string publicUrl,
        string? approverLogin = null,
        bool force = false,
        CancellationToken ct = default);

    Task<bool> IsServiceTokenMatchAsync(string token, CancellationToken ct = default);
}

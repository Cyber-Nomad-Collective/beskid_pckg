namespace Server.Services.AuthHub;

public sealed record AuthHubPairingStatus(bool Paired, string DefaultPublicUrl);

public sealed record AuthHubPairingResult(bool Ok, string? Error = null, bool AlreadyPaired = false);

public interface IAuthHubPairingService
{
    Task<AuthHubPairingStatus> GetStatusAsync(CancellationToken ct = default);

    Task<AuthHubPairingResult> CompletePairingAsync(
        string code,
        string publicUrl,
        CancellationToken ct = default);
}

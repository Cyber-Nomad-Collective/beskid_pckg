using FastEndpoints;
using Server.Services.AuthHub;

namespace Server.Features.Auth;

public sealed class AuthHubPairRequest
{
    public string Code { get; set; } = string.Empty;
    public string PublicUrl { get; set; } = string.Empty;
}

public sealed class AuthHubPairEndpoint : Endpoint<AuthHubPairRequest>
{
    public IAuthHubPairingService Pairing { get; set; } = default!;

    public override void Configure()
    {
        Post("/api/auth/hub/pair");
        Roles("SuperAdmin");
    }

    public override async Task HandleAsync(AuthHubPairRequest req, CancellationToken ct)
    {
        var result = await Pairing.CompletePairingAsync(req.Code, req.PublicUrl, ct);
        if (!result.Ok)
        {
            await Send.ResponseAsync(
                new { error = result.Error ?? "Pairing failed" },
                StatusCodes.Status400BadRequest,
                ct);
            return;
        }

        await Send.OkAsync(new { ok = true, alreadyPaired = result.AlreadyPaired }, ct);
    }
}

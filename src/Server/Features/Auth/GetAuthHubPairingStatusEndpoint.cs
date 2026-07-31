using FastEndpoints;
using Server.Services.AuthHub;

namespace Server.Features.Auth;

public sealed class GetAuthHubPairingStatusEndpoint(IAuthHubPairingService pairing)
    : EndpointWithoutRequest<AuthHubPairingStatus>
{
    public override void Configure()
    {
        Get("/api/auth/hub/pairing-status");
        AllowAnonymous();
        Summary(s => s.Summary = "Get auth hub pairing status for the pairing page.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var status = await pairing.GetStatusAsync(ct);
        await Send.OkAsync(status, ct);
    }
}

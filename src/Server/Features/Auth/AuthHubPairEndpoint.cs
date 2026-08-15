using FastEndpoints;
using Server.Services.AuthHub;

namespace Server.Features.Auth;

public sealed class AuthHubPairRequest
{
    public string Code { get; set; } = string.Empty;
    public string PublicUrl { get; set; } = string.Empty;
    public bool Force { get; set; }
}

public sealed class AuthHubPairEndpoint : Endpoint<AuthHubPairRequest>
{
    public IAuthHubPairingService Pairing { get; set; } = default!;

    public override void Configure()
    {
        Post("/auth/hub/pair");
    }

    public override async Task HandleAsync(AuthHubPairRequest req, CancellationToken ct)
    {
        var isRepair = await IsRepairRequest(ct);
        if (!isRepair)
        {
            if (User.Identity?.IsAuthenticated != true)
            {
                await Send.UnauthorizedAsync(ct);
                return;
            }

            if (!User.IsInRole("SuperAdmin"))
            {
                await Send.ForbiddenAsync(ct);
                return;
            }
        }

        var approverLogin = isRepair ? "service-repair" : null;
        var result = await Pairing.CompletePairingAsync(
            req.Code,
            req.PublicUrl,
            approverLogin,
            req.Force,
            ct);
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

    private async Task<bool> IsRepairRequest(CancellationToken ct)
    {
        var header = HttpContext?.Request?.Headers.Authorization.FirstOrDefault();
        var token = header?.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) == true
            ? header["Bearer ".Length..].Trim()
            : null;

        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        return await Pairing.IsServiceTokenMatchAsync(token, ct);
    }
}

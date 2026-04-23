using FastEndpoints;
using Microsoft.Extensions.Options;
using Server.Services;

namespace Server.Features.Public;

public sealed class GetCaptchaPublicConfigEndpoint(IOptions<CaptchaOptions> captchaOptions)
    : EndpointWithoutRequest<CaptchaPublicConfigResponse>
{
    public override void Configure()
    {
        Get("/public/captcha-config");
        AllowAnonymous();
        Summary(s => s.Summary = "Public keys required to render robot checks in the browser.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var o = captchaOptions.Value;
        await Send.OkAsync(new CaptchaPublicConfigResponse(o.TurnstileSiteKey ?? string.Empty), ct);
    }
}

public sealed record CaptchaPublicConfigResponse(string TurnstileSiteKey);

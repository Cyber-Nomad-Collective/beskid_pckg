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
        Summary(s => s.Summary = "reCAPTCHA Enterprise public site key when configured (widget is shown whenever a site key is set).");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var o = captchaOptions.Value;
        var hasSiteKey = !string.IsNullOrWhiteSpace(o.RecaptchaV3SiteKey);
        var siteKey = hasSiteKey ? o.RecaptchaV3SiteKey!.Trim() : null;
        await Send.OkAsync(new CaptchaPublicConfigResponse(hasSiteKey, siteKey), ct);
    }
}

public sealed record CaptchaPublicConfigResponse(bool CaptchaEnabled, string? RecaptchaSiteKey);

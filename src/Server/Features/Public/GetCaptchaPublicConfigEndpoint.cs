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
        Summary(s => s.Summary = "reCAPTCHA Enterprise public site key; captchaEnabled is true only when server-side verification is fully configured.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var o = captchaOptions.Value;
        var hasSiteKey = !string.IsNullOrWhiteSpace(o.RecaptchaV3SiteKey);
        var siteKey = hasSiteKey ? o.RecaptchaV3SiteKey!.Trim() : null;
        var captchaEnabled = hasSiteKey
            && !string.IsNullOrWhiteSpace(o.RecaptchaEnterpriseProjectId)
            && !string.IsNullOrWhiteSpace(o.RecaptchaEnterpriseApiKey);
        await Send.OkAsync(new CaptchaPublicConfigResponse(captchaEnabled, siteKey), ct);
    }
}

public sealed record CaptchaPublicConfigResponse(bool CaptchaEnabled, string? RecaptchaSiteKey);

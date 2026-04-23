using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace Server.Services;

public interface ICaptchaVerificationService
{
    /// <summary>When captcha is not configured, returns true. Otherwise validates the token.</summary>
    Task<bool> IsHumanAsync(string? captchaResponseToken, string? remoteIp, CancellationToken cancellationToken = default);
}

public sealed class CaptchaOptions
{
    public const string SectionName = "Captcha";

    /// <summary>Cloudflare Turnstile site key (public).</summary>
    public string? TurnstileSiteKey { get; set; }

    /// <summary>Cloudflare Turnstile secret key (server only).</summary>
    public string? TurnstileSecretKey { get; set; }
}

public sealed class CaptchaVerificationService(
    IHttpClientFactory httpClientFactory,
    Microsoft.Extensions.Options.IOptions<CaptchaOptions> options,
    ILogger<CaptchaVerificationService> logger) : ICaptchaVerificationService
{
    private readonly CaptchaOptions _options = options.Value;

    public async Task<bool> IsHumanAsync(string? captchaResponseToken, string? remoteIp, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.TurnstileSecretKey))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(captchaResponseToken))
        {
            return false;
        }

        try
        {
            var client = httpClientFactory.CreateClient(nameof(CaptchaVerificationService));
            var pairs = new List<KeyValuePair<string, string>>
            {
                new("secret", _options.TurnstileSecretKey),
                new("response", captchaResponseToken),
            };
            if (!string.IsNullOrWhiteSpace(remoteIp))
            {
                pairs.Add(new KeyValuePair<string, string>("remoteip", remoteIp));
            }

            using var content = new FormUrlEncodedContent(pairs);

            var verify = await client.PostAsync(
                "https://challenges.cloudflare.com/turnstile/v0/siteverify",
                content,
                cancellationToken);

            if (!verify.IsSuccessStatusCode)
            {
                logger.LogWarning("Turnstile HTTP {Status}", (int)verify.StatusCode);
                return false;
            }

            var body = await verify.Content.ReadFromJsonAsync<TurnstileVerifyResponse>(cancellationToken);
            return body?.Success == true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Turnstile verification failed.");
            return false;
        }
    }

    private sealed class TurnstileVerifyResponse
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }
    }
}

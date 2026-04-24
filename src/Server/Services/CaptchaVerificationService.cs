using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Server.Services;

public interface ICaptchaVerificationService
{
    /// <summary>
    /// Validates the token via reCAPTCHA Enterprise CreateAssessment when site key, project id, and API key are configured;
    /// otherwise returns true so callers can omit tokens when captcha is not in use.
    /// </summary>
    Task<bool> IsHumanAsync(string? token, string expectedAction, string? remoteIp, CancellationToken cancellationToken = default);
}

/// <summary>
/// reCAPTCHA Enterprise (v3-style scores). Restrict the API key in Google Cloud to reCAPTCHA Enterprise API only.
/// </summary>
public sealed class CaptchaOptions
{
    public const string SectionName = "Captcha";

    public string? RecaptchaV3SiteKey { get; set; }

    public string? RecaptchaEnterpriseProjectId { get; set; }

    /// <summary>Google Cloud API key with reCAPTCHA Enterprise API enabled (server-only).</summary>
    public string? RecaptchaEnterpriseApiKey { get; set; }

    /// <summary>Minimum risk score (0–1); submissions below this fail.</summary>
    public double MinimumScore { get; set; } = 0.5;
}

public sealed class CaptchaVerificationService(
    IHttpClientFactory httpClientFactory,
    Microsoft.Extensions.Options.IOptions<CaptchaOptions> options,
    ILogger<CaptchaVerificationService> logger) : ICaptchaVerificationService
{
    public const string RecaptchaEnterpriseHttpClientName = "RecaptchaEnterprise";
    private static readonly JsonSerializerOptions JsonWrite = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public async Task<bool> IsHumanAsync(string? token, string expectedAction, string? remoteIp, CancellationToken cancellationToken = default)
    {
        var o = options.Value;

        var verificationConfigured =
            !string.IsNullOrWhiteSpace(o.RecaptchaV3SiteKey)
            && !string.IsNullOrWhiteSpace(o.RecaptchaEnterpriseProjectId)
            && !string.IsNullOrWhiteSpace(o.RecaptchaEnterpriseApiKey);

        // Public UI only sends a token when captcha is advertised; when Enterprise is not wired,
        // callers send null — treat as "captcha off" so boards/reviews stay usable in dev and
        // partial misconfiguration does not hard-block all submissions.
        if (!verificationConfigured)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(token))
        {
            logger.LogWarning("reCAPTCHA rejected: missing token for action {Action}.", expectedAction);
            return false;
        }

        var siteKey = o.RecaptchaV3SiteKey!.Trim();
        var projectId = o.RecaptchaEnterpriseProjectId!.Trim();
        var apiKey = o.RecaptchaEnterpriseApiKey!.Trim();

        var client = httpClientFactory.CreateClient(RecaptchaEnterpriseHttpClientName);
        var url = $"v1/projects/{Uri.EscapeDataString(projectId)}/assessments?key={Uri.EscapeDataString(apiKey)}";

        var body = new CreateAssessmentRequestDto(new CreateAssessmentEventDto(
            token.Trim(),
            siteKey,
            expectedAction,
            string.IsNullOrWhiteSpace(remoteIp) ? null : remoteIp.Trim()));

        HttpResponseMessage response;
        try
        {
            response = await client.PostAsJsonAsync(url, body, JsonWrite, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "reCAPTCHA CreateAssessment request failed for action {Action}.", expectedAction);
            return false;
        }

        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogWarning(
                "reCAPTCHA CreateAssessment HTTP {Status} for action {Action}: {Body}",
                (int)response.StatusCode,
                expectedAction,
                err.Length > 200 ? err[..200] : err);
            return false;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var root = doc.RootElement;

        if (!root.TryGetProperty("tokenProperties", out var tokenProps))
        {
            logger.LogWarning("reCAPTCHA response missing tokenProperties for action {Action}.", expectedAction);
            return false;
        }

        var valid = tokenProps.TryGetProperty("valid", out var v) && v.GetBoolean();
        if (!valid)
        {
            var reason = tokenProps.TryGetProperty("invalidReason", out var ir) ? ir.GetString() : null;
            logger.LogWarning("reCAPTCHA token invalid for action {Action}: {Reason}.", expectedAction, reason ?? "unknown");
            return false;
        }

        if (tokenProps.TryGetProperty("action", out var actionEl))
        {
            var action = actionEl.GetString();
            if (!string.Equals(action, expectedAction, StringComparison.Ordinal))
            {
                logger.LogWarning(
                    "reCAPTCHA action mismatch: expected {Expected}, got {Actual}.",
                    expectedAction,
                    action ?? "(null)");
                return false;
            }
        }

        if (root.TryGetProperty("riskAnalysis", out var risk) && risk.TryGetProperty("score", out var scoreEl))
        {
            var score = scoreEl.GetDouble();
            var min = Math.Clamp(options.Value.MinimumScore, 0d, 1d);
            if (score < min)
            {
                logger.LogWarning("reCAPTCHA score {Score} below minimum {Min} for action {Action}.", score, min, expectedAction);
                return false;
            }
        }

        return true;
    }

    private sealed record CreateAssessmentEventDto(
        string Token,
        string SiteKey,
        string ExpectedAction,
        string? UserIpAddress);

    /// <summary>Maps to JSON <c>{"event":{...}}</c>.</summary>
    private sealed record CreateAssessmentRequestDto(
        [property: JsonPropertyName("event")] CreateAssessmentEventDto Event);
}

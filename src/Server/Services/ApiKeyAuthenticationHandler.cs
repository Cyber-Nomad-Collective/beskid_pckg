using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Server.Services;

public static class ApiKeyAuthenticationDefaults
{
    public const string Scheme = "ApiKey";
}

public sealed class ApiKeyAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IApiKeyValidator apiKeyValidator)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var presentedKey = ReadPresentedApiKey(Request);
        if (string.IsNullOrWhiteSpace(presentedKey))
        {
            return AuthenticateResult.NoResult();
        }

        var validated = await apiKeyValidator.ValidateAsync(presentedKey, Context.RequestAborted);
        if (validated is null)
        {
            return AuthenticateResult.Fail("Invalid API key.");
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, validated.UserId),
            new(ClaimTypes.Name, validated.UserId),
            new("api_key_id", validated.ApiKeyId.ToString("D")),
            new("auth_method", "api_key"),
        };

        foreach (var scope in validated.Scopes)
        {
            claims.Add(new Claim("scope", scope));
        }

        var identity = new ClaimsIdentity(claims, ApiKeyAuthenticationDefaults.Scheme);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, ApiKeyAuthenticationDefaults.Scheme);
        return AuthenticateResult.Success(ticket);
    }

    private static string? ReadPresentedApiKey(HttpRequest request)
    {
        if (request.Headers.TryGetValue("X-API-Key", out var apiKeyHeader))
        {
            var key = apiKeyHeader.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(key))
            {
                return key.Trim();
            }
        }

        if (request.Headers.TryGetValue("Authorization", out var authHeader))
        {
            var value = authHeader.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(value) && value.StartsWith("Bearer bpk_", StringComparison.OrdinalIgnoreCase))
            {
                return value["Bearer ".Length..].Trim();
            }
        }

        return null;
    }
}

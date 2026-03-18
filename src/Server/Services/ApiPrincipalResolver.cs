using System.Security.Claims;

namespace Server.Services;

public interface IApiPrincipalResolver
{
    Task<string?> ResolveUserIdAsync(HttpContext httpContext, CancellationToken cancellationToken = default);
}

public sealed class ApiPrincipalResolver(
    IApiKeyValidator apiKeyValidator) : IApiPrincipalResolver
{
    public async Task<string?> ResolveUserIdAsync(HttpContext httpContext, CancellationToken cancellationToken = default)
    {
        var userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!string.IsNullOrWhiteSpace(userId))
        {
            return userId;
        }

        var presentedKey = ReadPresentedApiKey(httpContext);
        if (string.IsNullOrWhiteSpace(presentedKey))
        {
            return null;
        }

        var validated = await apiKeyValidator.ValidateAsync(presentedKey, cancellationToken);
        return validated?.UserId;
    }

    private static string? ReadPresentedApiKey(HttpContext httpContext)
    {
        if (httpContext.Request.Headers.TryGetValue("X-API-Key", out var apiKeyHeader))
        {
            var key = apiKeyHeader.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(key))
            {
                return key.Trim();
            }
        }

        if (httpContext.Request.Headers.TryGetValue("Authorization", out var authHeader))
        {
            var value = authHeader.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(value) && value.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                return value["Bearer ".Length..].Trim();
            }
        }

        return null;
    }
}

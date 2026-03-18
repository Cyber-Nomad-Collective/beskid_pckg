using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using pckg.Data;

namespace Server.Services;

public sealed record ApiKeyValidationResult(
    Guid ApiKeyId,
    string UserId,
    IReadOnlyList<string> Scopes);

public interface IApiKeyValidator
{
    Task<ApiKeyValidationResult?> ValidateAsync(string presentedKey, CancellationToken cancellationToken = default);
}

public sealed class ApiKeyValidator(
    ApplicationDbContext dbContext,
    IPasswordHasher<ApiKeyEntity> apiKeyHasher) : IApiKeyValidator
{
    public async Task<ApiKeyValidationResult?> ValidateAsync(string presentedKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(presentedKey))
        {
            return null;
        }

        var trimmed = presentedKey.Trim();
        var prefix = trimmed.Length >= 10 ? trimmed[..10] : trimmed;
        var candidates = await dbContext.ApiKeys
            .AsNoTracking()
            .Where(k => k.RevokedAtUtc == null && k.Prefix == prefix)
            .ToListAsync(cancellationToken);

        foreach (var candidate in candidates)
        {
            var verification = apiKeyHasher.VerifyHashedPassword(candidate, candidate.KeyHash, trimmed);
            if (verification is PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded)
            {
                return new ApiKeyValidationResult(
                    candidate.Id,
                    candidate.UserId,
                    ParseScopes(candidate.ScopesCsv));
            }
        }

        return null;
    }

    private static IReadOnlyList<string> ParseScopes(string scopesCsv)
    {
        if (string.IsNullOrWhiteSpace(scopesCsv))
        {
            return [];
        }

        return scopesCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}

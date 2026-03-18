using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using pckg.Data;

namespace Server.Services;

public interface IApiKeyManagementService
{
    Task<IReadOnlyList<ApiKeyListItem>> ListAsync(string userId, CancellationToken cancellationToken = default);
    Task<ApiKeyCreateResult> CreateAsync(string userId, string name, IReadOnlyList<string> scopes, CancellationToken cancellationToken = default);
    Task<ApiKeyOperationResult> RevokeAsync(string userId, Guid keyId, CancellationToken cancellationToken = default);
}

public sealed record ApiKeyListItem(
    Guid Id,
    string Name,
    string Prefix,
    IReadOnlyList<string> Scopes,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? RevokedAtUtc);

public sealed record ApiKeyCreateResult(bool Success, string Message, string? PlainTextKey, ApiKeyListItem? Key);

public sealed record ApiKeyOperationResult(bool Success, string Message);

public sealed class ApiKeyManagementService(
    ApplicationDbContext dbContext,
    IPasswordHasher<ApiKeyEntity> apiKeyHasher) : IApiKeyManagementService
{
    private static readonly HashSet<string> AllowedScopes = ["publish", "read"];

    public async Task<IReadOnlyList<ApiKeyListItem>> ListAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return [];
        }

        var entities = await dbContext.ApiKeys
            .AsNoTracking()
            .Where(k => k.UserId == userId)
            .OrderByDescending(k => k.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return entities
            .Select(k => new ApiKeyListItem(
                k.Id,
                k.Name,
                k.Prefix,
                ParseScopes(k.ScopesCsv),
                k.CreatedAtUtc,
                k.RevokedAtUtc))
            .ToList();
    }

    public async Task<ApiKeyCreateResult> CreateAsync(string userId, string name, IReadOnlyList<string> scopes, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return new ApiKeyCreateResult(false, "Unauthorized.", null, null);
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            return new ApiKeyCreateResult(false, "Key name is required.", null, null);
        }

        var normalizedScopes = NormalizeScopes(scopes);
        if (normalizedScopes.Count == 0)
        {
            return new ApiKeyCreateResult(false, "At least one valid scope is required.", null, null);
        }

        var random = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(20)).ToLowerInvariant();
        var plain = $"bpk_{random}";

        var entity = new ApiKeyEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = name.Trim(),
            Prefix = plain[..10],
            ScopesCsv = string.Join(',', normalizedScopes),
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };
        entity.KeyHash = apiKeyHasher.HashPassword(entity, plain);

        dbContext.ApiKeys.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        var item = new ApiKeyListItem(
            entity.Id,
            entity.Name,
            entity.Prefix,
            normalizedScopes,
            entity.CreatedAtUtc,
            null);

        return new ApiKeyCreateResult(true, "API key created.", plain, item);
    }

    public async Task<ApiKeyOperationResult> RevokeAsync(string userId, Guid keyId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return new ApiKeyOperationResult(false, "Unauthorized.");
        }

        if (keyId == Guid.Empty)
        {
            return new ApiKeyOperationResult(false, "Invalid key id.");
        }

        var entity = await dbContext.ApiKeys.SingleOrDefaultAsync(k => k.Id == keyId && k.UserId == userId, cancellationToken);
        if (entity is null)
        {
            return new ApiKeyOperationResult(false, "API key not found.");
        }

        if (entity.RevokedAtUtc is null)
        {
            entity.RevokedAtUtc = DateTimeOffset.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return new ApiKeyOperationResult(true, "API key revoked.");
    }

    private static List<string> NormalizeScopes(IReadOnlyList<string> scopes)
        => scopes
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim().ToLowerInvariant())
            .Where(s => AllowedScopes.Contains(s))
            .Distinct(StringComparer.Ordinal)
            .ToList();

    private static IReadOnlyList<string> ParseScopes(string scopesCsv)
    {
        if (string.IsNullOrWhiteSpace(scopesCsv))
        {
            return [];
        }

        return scopesCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}

using FastEndpoints;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;
using pckg.Data;

namespace pckg.Features.ApiKeys;

public sealed class CreateApiKeyEndpoint(
    ApplicationDbContext dbContext,
    IPasswordHasher<ApiKeyEntity> apiKeyHasher)
    : Endpoint<CreateApiKeyRequest, CreateApiKeyResponse>
{
    public override void Configure()
    {
        Post("/keys");
        Options(x => x.RequireAuthorization());
        Summary(s =>
        {
            s.Summary = "Create a new API key (returns plaintext once).";
            s.Description = "Requires Identity authentication (cookie or bearer token).";
        });
    }

    public override async Task HandleAsync(CreateApiKeyRequest req, CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            HttpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await HttpContext.Response.WriteAsJsonAsync(new CreateApiKeyResponse(false, null, null, "Unauthorized."), ct);
            return;
        }

        if (string.IsNullOrWhiteSpace(req.Name))
        {
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await HttpContext.Response.WriteAsJsonAsync(new CreateApiKeyResponse(false, null, null, "Key name is required."), ct);
            return;
        }

        var random = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(20))
            .ToLowerInvariant();
        var plain = $"bpk_{random}";
        var entity = new ApiKeyEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = req.Name.Trim(),
            Prefix = plain[..10],
            ScopesCsv = string.Join(',', (req.Scopes ?? []).Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()).Distinct(StringComparer.OrdinalIgnoreCase)),
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };
        entity.KeyHash = apiKeyHasher.HashPassword(entity, plain);

        dbContext.ApiKeys.Add(entity);
        await dbContext.SaveChangesAsync(ct);

        var keyView = new ApiKeyView(
            entity.Id,
            entity.Name,
            entity.Prefix,
            string.IsNullOrWhiteSpace(entity.ScopesCsv)
                ? []
                : entity.ScopesCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            entity.CreatedAtUtc,
            entity.RevokedAtUtc);

        await Send.OkAsync(new CreateApiKeyResponse(true, plain, keyView, "API key created."), ct);
    }
}

public sealed record CreateApiKeyRequest(string Name, string[]? Scopes);

public sealed record ApiKeyView(
    Guid Id,
    string Name,
    string Prefix,
    IReadOnlyList<string> Scopes,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? RevokedAtUtc);

public sealed record CreateApiKeyResponse(bool Success, string? PlainTextKey, ApiKeyView? Key, string Message);

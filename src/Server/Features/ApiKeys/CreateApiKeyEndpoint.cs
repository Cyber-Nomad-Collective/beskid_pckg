using System.Security.Claims;
using FastEndpoints;
using Server.Services;

namespace Server.Features.ApiKeys;

public sealed class CreateApiKeyEndpoint(
    IApiKeyManagementService apiKeyManagementService)
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

        var requestedScopes = (req.Scopes ?? [])
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToArray();
        var result = await apiKeyManagementService.CreateAsync(userId, req.Name, requestedScopes, ct);

        if (!result.Success)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await HttpContext.Response.WriteAsJsonAsync(new CreateApiKeyResponse(false, null, null, result.Message), ct);
            return;
        }

        var keyView = new ApiKeyView(
            result.Key!.Id,
            result.Key.Name,
            result.Key.Prefix,
            result.Key.Scopes,
            result.Key.CreatedAtUtc,
            result.Key.RevokedAtUtc);

        await Send.OkAsync(new CreateApiKeyResponse(true, result.PlainTextKey, keyView, result.Message), ct);
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

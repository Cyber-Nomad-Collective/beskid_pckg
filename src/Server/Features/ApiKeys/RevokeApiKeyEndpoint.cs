using System.Security.Claims;
using FastEndpoints;
using Server.Services;

namespace Server.Features.ApiKeys;

public sealed class RevokeApiKeyEndpoint(IApiKeyManagementService apiKeyManagementService)
    : EndpointWithoutRequest<RevokeApiKeyResponse>
{
    public override void Configure()
    {
        Post("/keys/{KeyId}/revoke");
        Options(x => x.RequireAuthorization());
        Summary(s =>
        {
            s.Summary = "Revoke an API key owned by the current user.";
            s.Description = "Requires Identity authentication (cookie or bearer token).";
        });
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            HttpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await Send.OkAsync(new RevokeApiKeyResponse(false, "Unauthorized.", null), ct);
            return;
        }

        var keyId = Route<Guid>("KeyId");
        if (keyId == Guid.Empty)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await Send.OkAsync(new RevokeApiKeyResponse(false, "Invalid key id.", null), ct);
            return;
        }

        var result = await apiKeyManagementService.RevokeAsync(userId, keyId, ct);
        if (!result.Success)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status404NotFound;
            await Send.OkAsync(new RevokeApiKeyResponse(false, result.Message, null), ct);
            return;
        }

        await Send.OkAsync(new RevokeApiKeyResponse(true, result.Message, DateTimeOffset.UtcNow), ct);
    }
}

public sealed record RevokeApiKeyResponse(bool Success, string Message, DateTimeOffset? RevokedAtUtc);

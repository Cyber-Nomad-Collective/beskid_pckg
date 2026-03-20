using System.Security.Claims;
using FastEndpoints;
using Server.Services;

namespace Server.Features.ApiKeys;

public sealed class ListApiKeysEndpoint(IApiKeyManagementService apiKeyManagementService)
    : EndpointWithoutRequest<List<ApiKeysListResponse>>
{
    public override void Configure()
    {
        Get("/keys");
        Options(x => x.RequireAuthorization());
        Summary(s =>
        {
            s.Summary = "List API keys for current session user.";
            s.Description = "Requires Identity authentication (cookie or bearer token).";
        });
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            HttpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await HttpContext.Response.WriteAsJsonAsync(Array.Empty<ApiKeysListResponse>(), ct);
            return;
        }

        var keys = (await apiKeyManagementService.ListAsync(userId, ct))
            .Select(k => new ApiKeysListResponse(
                k.Id,
                k.Name,
                k.Prefix,
                k.Scopes,
                k.CreatedAtUtc,
                k.RevokedAtUtc))
            .ToList();

        await Send.OkAsync(keys, ct);
    }
}

public sealed record ApiKeysListResponse(
    Guid Id,
    string Name,
    string Prefix,
    IReadOnlyList<string> Scopes,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? RevokedAtUtc);

using System.Security.Claims;
using FastEndpoints;
using Server.Services;

namespace Server.Features.ApiKeys;

/// <summary>React projections over the canonical API-key management service.</summary>
public sealed class ListReactApiKeysEndpoint(IApiKeyManagementService apiKeys)
    : EndpointWithoutRequest<List<ReactApiKey>>
{
    public override void Configure()
    {
        Get("/api-keys");
        Options(options => options.RequireAuthorization());
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }

        var keys = await apiKeys.ListAsync(userId, ct);
        await Send.OkAsync(keys.Select(ReactApiKey.From).ToList(), ct);
    }
}

public sealed class CreateReactApiKeyEndpoint(IApiKeyManagementService apiKeys)
    : Endpoint<ReactCreateApiKeyRequest, ReactCreatedApiKey>
{
    public override void Configure()
    {
        Post("/api-keys");
        Options(options => options.RequireAuthorization());
    }

    public override async Task HandleAsync(ReactCreateApiKeyRequest req, CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }

        var result = await apiKeys.CreateAsync(userId, req.Name, req.Scopes ?? [], ct);
        if (!result.Success || result.Key is null || result.PlainTextKey is null)
        {
            await Send.ErrorsAsync(StatusCodes.Status400BadRequest, ct);
            return;
        }

        await Send.OkAsync(new ReactCreatedApiKey(ReactApiKey.From(result.Key), result.PlainTextKey), ct);
    }
}

public sealed class RevokeReactApiKeyEndpoint(IApiKeyManagementService apiKeys)
    : EndpointWithoutRequest
{
    public override void Configure()
    {
        Delete("/api-keys/{keyId:guid}");
        Options(options => options.RequireAuthorization());
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var keyId = Route<Guid>("keyId");
        if (string.IsNullOrWhiteSpace(userId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }

        var result = await apiKeys.RevokeAsync(userId, keyId, ct);
        if (!result.Success)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.NoContentAsync(ct);
    }
}

public sealed record ReactCreateApiKeyRequest(string Name, string[]? Scopes);
public sealed record ReactCreatedApiKey(ReactApiKey Key, string PlainTextKey);
public sealed record ReactApiKey(Guid Id, string Name, string Prefix, IReadOnlyList<string> Scopes, DateTimeOffset CreatedAtUtc, DateTimeOffset? RevokedAtUtc)
{
    public static ReactApiKey From(ApiKeyListItem item) => new(item.Id, item.Name, item.Prefix, item.Scopes, item.CreatedAtUtc, item.RevokedAtUtc);
}
